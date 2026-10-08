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
    private readonly IAuditService _auditService;

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
        IAuditService auditService,
        IConfiguration configuration)
    {
        _auditService = auditService;
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

    /// <summary>At most this many remembered devices per user; the oldest are forgotten.</summary>
    public const int MaxRememberedDevices = 5;

    private const string DeviceNotRecognisedMessage =
        "This device is no longer remembered. Please enter your authorization code.";

    /// <summary>
    /// The user signing in: by a remembered device's key (still valid, feature on, account
    /// active) or by authorization code. A device key never falls back to anything else.
    /// </summary>
    private async Task<(User? User, TrustedDevice? Device)> FindSignInAsync(string? authorizationCode, string? deviceToken)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return (await FindActiveUserAsync(authorizationCode), null);

        if ((await _settingsService.GetAsync()).RememberDeviceDays <= 0)
            return (null, null);

        var hash = _codeHasher.HashDeviceToken(deviceToken);
        var now = DateTime.UtcNow;

        var device = await _dbContext.TrustedDevices
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.TokenHash == hash && d.ExpiresAt > now);

        return device == null || !device.User.IsActive
            ? (null, null)
            : (device.User, device);
    }

    /// <summary>On a remembered device: the first name and the end of the phone number, for "Welcome back".</summary>
    public async Task<RememberedDeviceDto?> GetRememberedDeviceAsync(string? deviceToken)
    {
        var (user, device) = await FindSignInAsync(null, deviceToken);

        if (user == null || device == null)
            return null;

        var mobile = new string((user.MobileNumber ?? string.Empty).Where(char.IsDigit).ToArray());

        return new RememberedDeviceDto
        {
            FirstName = (user.FullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
            MaskedMobile = mobile.Length >= 3 ? "•••" + mobile[^3..] : string.Empty
        };
    }

    /// <summary>"Not you? Forget this device" on the sign-in page.</summary>
    public async Task ForgetDeviceAsync(string? deviceToken)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return;

        var hash = _codeHasher.HashDeviceToken(deviceToken);
        var device = await _dbContext.TrustedDevices.FirstOrDefaultAsync(d => d.TokenHash == hash);

        if (device == null)
            return;

        _dbContext.TrustedDevices.Remove(device);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(device.UserId, $"Forgot a remembered device: {device.Name}", "User", device.UserId.ToString());
    }

    /// <summary>Remembers this browser for the user; returns the device key (given only to the browser, once).</summary>
    private async Task<(string Token, DateTime ExpiresAt)> RememberDeviceAsync(User user, string? deviceName, int days)
    {
        var now = DateTime.UtcNow;

        // Keep the newest few; expired ones go too.
        var existing = await _dbContext.TrustedDevices
            .Where(d => d.UserId == user.Id)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        _dbContext.TrustedDevices.RemoveRange(
            existing.Where((d, i) => d.ExpiresAt <= now || i >= MaxRememberedDevices - 1));

        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var name = string.IsNullOrWhiteSpace(deviceName) ? "A web browser" : deviceName.Trim();
        var expiresAt = now.AddDays(days);

        _dbContext.TrustedDevices.Add(new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _codeHasher.HashDeviceToken(token),
            Name = name.Length > 100 ? name[..100] : name,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            LastUsedAt = now
        });

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(user.Id, $"Remembered a device for {days} days: {name}", "User", user.Id.ToString());

        return (token, expiresAt);
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
        var (user, _) = await FindSignInAsync(request.AuthorizationCode, request.DeviceToken);

        if (user == null)
        {
            var byDevice = !string.IsNullOrWhiteSpace(request.DeviceToken);

            return new GenerateOtpResultDto
            {
                Success = false,
                DeviceNotRecognised = byDevice,
                Message = byDevice
                    ? DeviceNotRecognisedMessage
                    : "That authorization code was not recognised. Check it and try again."
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
            $"{(await _settingsService.GetBrandingAsync()).ShortName} code: {otp}. It expires in {settings.OtpExpiryMinutes} minutes. " +
            "Never share this code. Campaign staff will never ask for it.");

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

        var (user, device) = await FindSignInAsync(request.AuthorizationCode, request.DeviceToken);

        if (user == null)
        {
            return string.IsNullOrWhiteSpace(request.DeviceToken)
                ? Fail("That authorization code was not recognised. Go back and enter it again.")
                : new LoginResponseDto { Success = false, DeviceNotRecognised = true, Message = DeviceNotRecognisedMessage };
        }

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

        // Remembered device: note its use. "Remember me" ticked: remember this browser.
        string? newDeviceToken = null;
        DateTime? deviceExpiresAt = null;

        if (device != null)
        {
            await _dbContext.TrustedDevices
                .Where(d => d.Id == device.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastUsedAt, DateTime.UtcNow));
        }
        else if (request.RememberDevice)
        {
            var days = (await _settingsService.GetAsync()).RememberDeviceDays;

            if (days > 0)
                (newDeviceToken, deviceExpiresAt) = await RememberDeviceAsync(user, request.DeviceName, days);
        }

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
            Roles = roles,
            DeviceToken = newDeviceToken,
            DeviceExpiresAt = deviceExpiresAt
        };
    }
}
