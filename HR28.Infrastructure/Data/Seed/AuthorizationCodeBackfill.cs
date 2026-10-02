using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Data.Seed;

/// <summary>
/// One-time conversion: hashes any plain-text authorization codes and erases them.
/// Safe to run on every startup; it only touches users that still have a plain code.
/// Existing users keep the same code, so nobody needs a reset.
/// </summary>
public static class AuthorizationCodeBackfill
{
    public static async Task<int> RunAsync(
        HR28DbContext context,
        IAuthorizationCodeHasher hasher)
    {
        var users = await context.Users
            .Where(u => u.AuthorizationCode != null && u.AuthorizationCode != "")
            .ToListAsync();

        if (users.Count == 0)
            return 0;

        foreach (var user in users)
        {
            var hash = hasher.Hash(user.AuthorizationCode!);

            user.AuthorizationCodeHash = hash;
            user.AuthorizationCode = null;

            context.AuthorizationCodeHistories.Add(new AuthorizationCodeHistory
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                AuthorizationCodeHash = hash,
                CreatedAt = DateTime.UtcNow,
                Reason = "Existing code converted to secure storage"
            });
        }

        await context.SaveChangesAsync();

        return users.Count;
    }

    /// <summary>
    /// Erases SMS codes stored as plain digits before codes were hashed, and marks
    /// them used. Hashes are 64 hex characters; anything else is a legacy code.
    /// Safe to run on every startup. Anyone mid-sign-in just asks for a new code.
    /// </summary>
    public static Task<int> ClearPlainOtpCodesAsync(HR28DbContext context) =>
        context.OtpRequests
            .Where(o => o.OtpCode != "" && o.OtpCode.Length != 64)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.OtpCode, "")
                .SetProperty(o => o.IsUsed, true));
}
