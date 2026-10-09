using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HR28.Domain.Entities;
using HR28.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HR28.Tests;

/// <summary>
/// New users start inactive with no code; they can be activated only with a role; the
/// first activation creates the code and sends it in a welcome SMS (shown to the
/// administrator only if the SMS fails).
/// </summary>
[Collection(ApiCollection.Name)]
public class UserActivationTests
{
    private readonly Hr28ApiFactory _factory;

    public UserActivationTests(Hr28ApiFactory factory) => _factory = factory;

    private sealed record Created(Guid Id, bool IsActive, string? AuthorizationCode);

    private sealed record Activation(bool Activated, bool FirstActivation, bool SmsSent, string? AuthorizationCode);

    private static int _next;

    private async Task<(HttpClient Admin, Created User, string Mobile)> CreateAsync()
    {
        var admin = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Super Administrator"]));
        var n = Interlocked.Increment(ref _next);
        var nid = $"A9{Random.Shared.Next(10000, 99999)}";
        var mobile = $"7{Random.Shared.Next(100000, 999999)}";

        var response = await admin.PostAsJsonAsync("api/Users", new
        {
            nationalId = nid,
            fullName = $"Aminath Test{n}",
            mobileNumber = mobile,
            address = "",
            email = "",
            designation = "",
            remarks = ""
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (admin, (await response.Content.ReadFromJsonAsync<Created>())!, mobile);
    }

    private async Task GiveRoleAsync(Guid userId, string role)
    {
        await using var db = _factory.NewDbContext();
        var roleId = await db.Roles.Where(r => r.Name == role).Select(r => r.Id).SingleAsync();
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_user_can_be_created_with_only_the_required_fields()
    {
        var admin = await _factory.ClientForAsync(await _factory.Data.UserAsync(["Super Administrator"]));

        var response = await admin.PostAsJsonAsync("api/Users", new
        {
            nationalId = $"A8{Random.Shared.Next(10000, 99999)}",
            fullName = "Only Required",
            mobileNumber = $"7{Random.Shared.Next(100000, 999999)}",
            address = (string?)null,
            email = (string?)null,
            designation = (string?)null,
            remarks = (string?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_new_user_starts_inactive_without_a_code()
    {
        var (_, user, _) = await CreateAsync();

        Assert.False(user.IsActive);
        Assert.True(string.IsNullOrEmpty(user.AuthorizationCode));

        await using var db = _factory.NewDbContext();
        var stored = await db.Users.SingleAsync(u => u.Id == user.Id);
        Assert.False(stored.IsActive);
        Assert.Null(stored.AuthorizationCodeHash);
    }

    [Fact]
    public async Task Activation_needs_at_least_one_role()
    {
        var (admin, user, _) = await CreateAsync();

        var response = await admin.PutAsJsonAsync($"api/Users/{user.Id}/active", new { isActive = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at least one role", await response.Content.ReadAsStringAsync());

        await using var db = _factory.NewDbContext();
        Assert.False((await db.Users.SingleAsync(u => u.Id == user.Id)).IsActive);
    }

    [Fact]
    public async Task First_activation_sends_a_welcome_SMS_with_a_working_code()
    {
        var (admin, user, mobile) = await CreateAsync();
        await GiveRoleAsync(user.Id, "Collector");

        var response = await admin.PutAsJsonAsync($"api/Users/{user.Id}/active", new { isActive = true });
        var result = await response.Content.ReadFromJsonAsync<Activation>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(result!.Activated && result.FirstActivation && result.SmsSent);
        Assert.Null(result.AuthorizationCode);   // sent by SMS, never shown to the administrator

        var sms = _factory.Sms.MessagesTo(mobile).Last();
        Assert.Contains("Welcome, Aminath", sms);
        var code = Regex.Match(sms, @"[A-Z2-9]{4}-[A-Z2-9]{4}").Value;
        Assert.False(string.IsNullOrEmpty(code));

        // The code from the SMS signs in.
        var signIn = await _factory.AnonymousClient().PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = code });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        await using var db = _factory.NewDbContext();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == user.Id.ToString() && a.Action == "Welcome SMS with the authorization code sent"));
    }

    [Fact]
    public async Task Activating_again_later_keeps_the_code_and_sends_no_new_one()
    {
        var (admin, user, mobile) = await CreateAsync();
        await GiveRoleAsync(user.Id, "Collector");
        await admin.PutAsJsonAsync($"api/Users/{user.Id}/active", new { isActive = true });

        string hashBefore;
        await using (var db = _factory.NewDbContext())
            hashBefore = (await db.Users.SingleAsync(u => u.Id == user.Id)).AuthorizationCodeHash!;

        await admin.PutAsJsonAsync($"api/Users/{user.Id}/active", new { isActive = false });
        var again = await (await admin.PutAsJsonAsync($"api/Users/{user.Id}/active", new { isActive = true }))
            .Content.ReadFromJsonAsync<Activation>();

        Assert.True(again!.Activated);
        Assert.False(again.FirstActivation);
        Assert.Contains("active again", _factory.Sms.MessagesTo(mobile).Last());
        Assert.DoesNotMatch(@"[A-Z2-9]{4}-[A-Z2-9]{4}", _factory.Sms.MessagesTo(mobile).Last());

        await using var db2 = _factory.NewDbContext();
        Assert.Equal(hashBefore, (await db2.Users.SingleAsync(u => u.Id == user.Id)).AuthorizationCodeHash);
    }

    [Fact]
    public async Task Activating_through_the_edit_form_follows_the_same_rules()
    {
        var (admin, user, mobile) = await CreateAsync();

        // Fill the real ID card so the edit validates.
        string nid;
        await using (var db = _factory.NewDbContext())
            nid = (await db.Users.SingleAsync(u => u.Id == user.Id)).NationalId;

        object WithNid(bool active) => new
        {
            nationalId = nid, fullName = "Aminath Edited", mobileNumber = mobile,
            address = "", email = "", designation = "", remarks = "", isActive = active
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"api/Users/{user.Id}", WithNid(true))).StatusCode);

        await GiveRoleAsync(user.Id, "Collector");

        var result = await (await admin.PutAsJsonAsync($"api/Users/{user.Id}", WithNid(true))).Content.ReadFromJsonAsync<Activation>();
        Assert.True(result!.Activated && result.FirstActivation && result.SmsSent);
        Assert.Contains("Your authorization code is", _factory.Sms.MessagesTo(mobile).Last());
    }
}
