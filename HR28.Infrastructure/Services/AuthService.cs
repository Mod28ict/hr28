using HR28.Application.DTOs.Auth;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using HR28.Infrastructure.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly HR28DbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly ISystemSettingsService _settingsService;
    private readonly IAuthorizationCodeHasher _codeHasher;

    public AuthService(
        HR28DbContext dbContext,
        ITokenService tokenService,
        ISystemSettingsService settingsService,
        IAuthorizationCodeHasher codeHasher)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _settingsService = settingsService;
        _codeHasher = codeHasher;
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

        var otpRequest = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpCode = otp,
            ExpiresAt = DateTime.UtcNow.AddMinutes(settings.OtpExpiryMinutes),
            FailedAttempts = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.OtpRequests.Add(otpRequest);

        await _dbContext.SaveChangesAsync();

        Console.WriteLine(
            $"OTP for {user.FullName}: {otp}");

        return new GenerateOtpResultDto
        {
            Success = true,
            ExpiresInSeconds = settings.OtpExpiryMinutes * 60
        };
    }

    /// <summary>Constant-time comparison so response timing doesn't leak how many digits matched.</summary>
    private static bool CodesMatch(string expected, string? actual)
    {
        var a = System.Text.Encoding.UTF8.GetBytes(expected ?? string.Empty);
        var b = System.Text.Encoding.UTF8.GetBytes((actual ?? string.Empty).Trim());

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
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

        if (otpRequest.FailedAttempts >= maxAttempts)
            return Fail("Too many wrong codes. Go back and ask for a new code.");

        if (!CodesMatch(otpRequest.OtpCode, request.OtpCode))
        {
            otpRequest.FailedAttempts++;

            // Account-wide count across codes: stops guessing by requesting fresh codes.
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= LockoutAfterFailedCodes)
            {
                user.LockedUntilUtc = now.AddMinutes(LockoutMinutes);
                user.FailedLoginAttempts = 0;

                await _dbContext.SaveChangesAsync();

                return Fail(WaitMessage(LockoutMinutes * 60));
            }

            await _dbContext.SaveChangesAsync();

            var left = maxAttempts - otpRequest.FailedAttempts;

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

        user.LastLoginAt = DateTime.UtcNow;
        user.FailedLoginAttempts = 0;
        user.LockedUntilUtc = null;
        await _dbContext.SaveChangesAsync();
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
