using System.Net;
using System.Net.Http.Json;
using HR28.Tests.Infrastructure;

namespace HR28.Tests;

/// <summary>
/// ASVS review L3 (2026-10-17): only 8-character authorization codes sign in; the older
/// 5-character codes were retired.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthorizationCodeLengthTests
{
    private readonly Hr28ApiFactory _factory;

    public AuthorizationCodeLengthTests(Hr28ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_old_five_character_code_no_longer_signs_in()
    {
        var oldCode = $"Q{Guid.NewGuid():N}"[..5].ToUpperInvariant();
        var user = await _factory.Data.UserAsync(["Collector"], authorizationCode: oldCode);

        var response = await _factory.AnonymousClient()
            .PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = oldCode });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Sms.MessagesTo(user.MobileNumber));
    }

    [Fact]
    public async Task An_eight_character_code_still_signs_in_with_or_without_the_dash()
    {
        var code = $"E{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        await _factory.Data.UserAsync(["Collector"], authorizationCode: code);

        var response = await _factory.AnonymousClient()
            .PostAsJsonAsync("api/Auth/generate-otp", new { authorizationCode = $"{code[..4]}-{code[4..]}".ToLowerInvariant() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
