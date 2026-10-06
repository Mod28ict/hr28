using HR28.Application.DTOs.Auth;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using HR28.Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HR28.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly HR28DbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly ISystemSettingsService _settingsService;
    private readonly IAuthorizationCodeHasher _codeHasher;
    private readonly ISmsSender _smsSender;

    /// <summary>
    /// Development only, until a real SMS provider is connected: keep SMS codes readable
    /// in OtpRequests so testers can sign in. The API refuses to start with this on
    /// outside Development. Remove the setting to store hashes again.
    /// </summary>
    public const string StoreReadableOtpSetting = "Security:StoreReadableOtpCodes";

    private readonly bool _storeReadableOtp;

    public AuthService(
        HR28DbContext dbContext,
        ITokenService tokenService,
        ISystemSettingsService settingsService,
        IAuthorizationCodeHasher codeHasher,
        ISmsSender smsSender,
        IConfiguration configuration)
    {
        _storeReadableOtp = bool.TryParse(configuration[StoreReadableOtpSetting], out var readable) && readable;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _settingsService = settingsService;
        _codeHasher = codeHasher;
        _smsSender = smsSender;
    }

    /// <summary>Finds an active user by authorization code (compared by keyed hash only).</summary>
    private async Task<User?> FindActiveUserAsync(string? authorizationCode)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
            return null;

        var hash = _codeHasher.Hash(authorizationCode);

        return await _dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.AuthorizationCodeHash == hash &&
                u.IsActive);
    }

    // Per-account protection. These work no matter which IP the requests come from;
    // per-IP limits are applied separately by the API rate limiter.
    public const int ResendCooldownSeconds = 60;
    public const int MaxCodesPerHour = 5;
    public const int LockoutAfterFailedCodes = 10;
    public const int LockoutMinutes = 15;

    private static string WaitMessage(int seconds) =>
        seconds >= 90
            ? $"Too many attempts. Please wait {(int)Math.Ceiling(seconds / 60.0)} minutes and try again."
            : $"Please wait {seconds} seconds before asking for a new code.";

    private static int SecondsUntil(DateTime utc) =>
        Math.Max(1, (int)Math.Ceiling((utc - DateTime.UtcNow).TotalSeconds));

    public async Task<GenerateOtpResultDto> GenerateOtpAsync(
        GenerateOtpRequestDto request)
    {
        var user = await FindActiveUserAsync(request.AuthorizationCode);

        if (user == null)
        {
            return new GenerateOtpResultDto
            {
                Success = false,
                Message = "That authorization code was not recognised. Check it and try again."
            };
        }

        var now = DateTime.UtcNow;

        if (user.LockedUntilUtc > now)
        {
            var wait = SecondsUntil(user.LockedUntilUtc.Value);
            return new GenerateOtpResultDto { IsThrottled = true, RetryAfterSeconds = wait, Message = WaitMessage(wait) };
        }

        var recent = await _dbContext.OtpRequests
            .Where(o => o.UserId == user.Id && o.CreatedAt > now.AddHours(-1))
            .Select(o => o.CreatedAt)
            .ToListAsync();

        if (recent.Count > 0 && recent.Max() > now.AddSeconds(-ResendCooldownSeconds))
        {
            var wait = SecondsUntil(recent.Max().AddSeconds(ResendCooldownSeconds));
            return new GenerateOtpResultDto { IsThrottled = true, RetryAfterSeconds = wait, Message = WaitMessage(wait) };
        }

        if (recent.Count >= MaxCodesPerHour)
        {
            var wait = SecondsUntil(recent.Min().AddHours(1));
            return new GenerateOtpResultDto { IsThrottled = true, RetryAfterSeconds = wait, Message = WaitMessage(wait) };
        }

        var otp = OtpGenerator.Generate();

        var settings = await _settingsService.GetAsync();

        var otpRequestId = Guid.NewGuid();

        var otpRequest = new OtpRequest
        {
            Id = otpRequestId,
            UserId = user.Id,

            // Normally only a keyed hash is stored; the digits exist only in the SMS.
            OtpCode = _storeReadableOtp ? otp : _codeHasher.HashOtp(otpRequestId, otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(settings.OtpExpiryMinutes),
            FailedAttempts = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.OtpRequests.Add(otpRequest);

        await _dbContext.SaveChangesAsync();

        var sent = await _smsSender.SendAsync(
            user.MobileNumber,
            $"HR28 code: {otp}. It expires in {settings.OtpExpiryMinutes} minutes. " +
            "Never share this code. HR28 staff will never ask for it.");

        if (!sent)
        {
            // A code nobody received must not stay usable.
            otpRequest.IsUsed = true;
            await _dbContext.SaveChangesAsync();

            return new GenerateOtpResultDto
            {
                Success = false,
                Message = "We couldn't send your code right now. Please try again later or contact your administrator."
            };
        }

        return new GenerateOtpResultDto
        {
            Success = true,
            ExpiresInSeconds = settings.OtpExpiryMinutes * 60
        };
    }

    /// <summary>
    /// Hashes the entered code the same way and compares in constant time,
    /// so response timing doesn't leak anything about the stored hash.
    /// </summary>
    private bool CodesMatch(OtpRequest otpRequest, string? entered)
    {
        var stored = otpRequest.OtpCode ?? string.Empty;

        // A readable (Development) code is 6 digits; a hash is 64 hex characters.
        var enteredForm = stored.Length == OtpGenerator.Length
            ? (entered ?? string.Empty).Trim()
            : _codeHasher.HashOtp(otpRequest.Id, entered ?? string.Empty);

        var expected = System.Text.Encoding.ASCII.GetBytes(stored);
        var actual = System.Text.Encoding.ASCII.GetBytes(enteredForm);

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public async Task<LoginResponseDto> VerifyOtpAsync(
        VerifyOtpRequestDto request)
    {
        LoginResponseDto Fail(string message) =>
            new() { Success = false, Message = message };

        var user = await FindActiveUserAsync(request.AuthorizationCode);

        if (user == null)
            return Fail("That authorization code was not recognised. Go back and enter it again.");

        var now = DateTime.UtcNow;

        if (user.LockedUntilUtc > now)
            return Fail(WaitMessage(SecondsUntil(user.LockedUntilUtc.Value)));

        var otpRequest = await _dbContext.OtpRequests
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRequest == null || otpRequest.IsUsed)
            return Fail("This code is no longer valid. Go back and ask for a new code.");

        if (otpRequest.ExpiresAt < now)
            return Fail("This code has expired. Go back and ask for a new code.");

        var maxAttempts = (await _settingsService.GetAsync()).OtpMaxAttempts;

        // Every guess first takes one attempt in a single SQL statement, so requests sent
        // in parallel can't all slip under the limit: at most maxAttempts guesses are ever
        // checked against one code. (FailedAttempts therefore also counts the correct guess;
        // the code is used up at that point anyway.)
        var reserved = await _dbContext.OtpRequests
            .Where(o => o.Id == otpRequest.Id &&
                        !o.IsUsed &&
                        o.ExpiresAt >= now &&
                        o.FailedAttempts < maxAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.FailedAttempts, o => o.FailedAttempts + 1));

        if (reserved != 1)
        {
            var state = await _dbContext.OtpRequests
                .AsNoTracking()
                .Where(o => o.Id == otpRequest.Id)
                .Select(o => new { o.IsUsed, o.ExpiresAt })
                .FirstAsync();

            return Fail(state.IsUsed
                ? "This code is no longer valid. Go back and ask for a new code."
                : state.ExpiresAt < now
                    ? "This code has expired. Go back and ask for a new code."
                    : "Too many wrong codes. Go back and ask for a new code.");
        }

        if (!CodesMatch(otpRequest, request.OtpCode))
        {
            // Account-wide count across codes (stops guessing by requesting fresh codes),
            // also in one statement; reaching the limit locks sign-in and restarts the count.
            DateTime? lockUntil = now.AddMinutes(LockoutMinutes);

            await _dbContext.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(
                        u => u.LockedUntilUtc,
                        u => u.FailedLoginAttempts + 1 >= LockoutAfterFailedCodes ? lockUntil : u.LockedUntilUtc)
                    .SetProperty(
                        u => u.FailedLoginAttempts,
                        u => u.FailedLoginAttempts + 1 >= LockoutAfterFailedCodes ? 0 : u.FailedLoginAttempts + 1));

            var lockedUntil = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == user.Id)
                .Select(u => u.LockedUntilUtc)
                .FirstAsync();

            if (lockedUntil > now)
                return Fail(WaitMessage(SecondsUntil(lockedUntil.Value)));

            var attemptsUsed = await _dbContext.OtpRequests
                .AsNoTracking()
                .Where(o => o.Id == otpRequest.Id)
                .Select(o => o.FailedAttempts)
                .FirstAsync();

            var left = maxAttempts - attemptsUsed;

            return Fail(left > 0
                ? $"That code is not correct. You have {left} {(left == 1 ? "try" : "tries")} left."
                : "Too many wrong codes. Go back and ask for a new code.");
        }

        // Single use: only one request can flip IsUsed from false to true.
        var consumed = await _dbContext.OtpRequests
            .Where(o => o.Id == otpRequest.Id && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));

        if (consumed != 1)
            return Fail("This code is no longer valid. Go back and ask for a new code.");

        var token = await _tokenService.GenerateTokenAsync(user);

        // In one statement too: the counters were changed outside change tracking above,
        // so saving the tracked user would not reliably reset them.
        await _dbContext.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LastLoginAt, DateTime.UtcNow)
                .SetProperty(u => u.FailedLoginAttempts, 0)
                .SetProperty(u => u.LockedUntilUtc, (DateTime?)null));

        var roles = HR28.Application.DTOs.Users.RoleOrder.Sort(
            await _dbContext.UserRoles
                .Where(x => x.UserId == user.Id)
                .Select(x => x.Role.Name)
                .ToListAsync());


        return new LoginResponseDto
        {
            Success = true,
            Message = "OTP verified successfully.",
            Token = token,

            UserId = user.Id,
            FullName = user.FullName,
            RoleName = roles.FirstOrDefault() ?? string.Empty,
            Roles = roles
        };
    }
}
