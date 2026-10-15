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
    // per-IP limits are applied separately by the API rate limiter. Sign-in and
    // "confirm it's you" codes share them.
    public const int ResendCooldownSeconds = 60;
    public const int MaxCodesPerHour = 5;
    public const int LockoutAfterFailedCodes = 10;
    public const int LockoutMinutes = 15;

    /// <summary>At most this many known browsers per user for sign-in alerts; the least recent are forgotten.</summary>
    public const int MaxKnownDevices = 20;

    private static string WaitMessage(int seconds) =>
        seconds >= 90
            ? $"Too many attempts. Please wait {(int)Math.Ceiling(seconds / 60.0)} minutes and try again."
            : $"Please wait {seconds} seconds before asking for a new code.";

    private static int SecondsUntil(DateTime utc) =>
        Math.Max(1, (int)Math.Ceiling((utc - DateTime.UtcNow).TotalSeconds));

    private static string Masked(string? mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length >= 3 ? "•••" + digits[^3..] : string.Empty;
    }

    /// <summary>Seconds to wait before this account may get another code, or null if it may now.</summary>
    private async Task<int?> CodeWaitSecondsAsync(User user)
    {
        var now = DateTime.UtcNow;

        if (user.LockedUntilUtc > now)
            return SecondsUntil(user.LockedUntilUtc.Value);

        var recent = await _dbContext.OtpRequests
            .Where(o => o.UserId == user.Id && o.CreatedAt > now.AddHours(-1))
            .Select(o => o.CreatedAt)
            .ToListAsync();

        if (recent.Count > 0 && recent.Max() > now.AddSeconds(-ResendCooldownSeconds))
            return SecondsUntil(recent.Max().AddSeconds(ResendCooldownSeconds));

        if (recent.Count >= MaxCodesPerHour)
            return SecondsUntil(recent.Min().AddHours(1));

        return null;
    }

    /// <summary>Stores a new code for the purpose (keyed hash only) and sends the SMS; false if it couldn't be sent.</summary>
    private async Task<bool> IssueCodeAsync(User user, string purpose, Func<string, int, Task<string>> message)
    {
        var otp = OtpGenerator.Generate();
        var settings = await _settingsService.GetAsync();
        var otpRequestId = Guid.NewGuid();

        var otpRequest = new OtpRequest
        {
            Id = otpRequestId,
            UserId = user.Id,
            Purpose = purpose,

            // Normally only a keyed hash is stored; the digits exist only in the SMS.
            OtpCode = _storeReadableOtp ? otp : _codeHasher.HashOtp(otpRequestId, otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(settings.OtpExpiryMinutes),
            FailedAttempts = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.OtpRequests.Add(otpRequest);
        await _dbContext.SaveChangesAsync();

        var sent = await _smsSender.SendAsync(user.MobileNumber, await message(otp, settings.OtpExpiryMinutes));

        if (!sent)
        {
            // A code nobody received must not stay usable.
            otpRequest.IsUsed = true;
            await _dbContext.SaveChangesAsync();
        }

        return sent;
    }

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

        if (await CodeWaitSecondsAsync(user) is { } wait)
            return new GenerateOtpResultDto { IsThrottled = true, RetryAfterSeconds = wait, Message = WaitMessage(wait) };

        var sent = await IssueCodeAsync(user, OtpRequest.SignInPurpose, async (otp, minutes) =>
            $"{(await _settingsService.GetBrandingAsync()).ShortName} code: {otp}. It expires in {minutes} minutes. " +
            "Never share this code. Campaign staff will never ask for it.");

        if (!sent)
        {
            return new GenerateOtpResultDto
            {
                Success = false,
                Message = "We couldn't send your code right now. Please try again later or contact your administrator."
            };
        }

        return new GenerateOtpResultDto
        {
            Success = true,
            ExpiresInSeconds = (await _settingsService.GetAsync()).OtpExpiryMinutes * 60
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

    /// <summary>
    /// Checks the entered code against the user's latest code for this purpose and uses it up
    /// when it matches. Returns null on success, otherwise the message to show. A sign-in code
    /// never works as a "confirm it's you" code, nor the other way round.
    /// </summary>
    private async Task<string?> UseCodeAsync(User user, string purpose, string? entered, string askAgain)
    {
        var now = DateTime.UtcNow;

        if (user.LockedUntilUtc > now)
            return WaitMessage(SecondsUntil(user.LockedUntilUtc.Value));

        var otpRequest = await _dbContext.OtpRequests
            .Where(o => o.UserId == user.Id && o.Purpose == purpose)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRequest == null || otpRequest.IsUsed)
            return $"This code is no longer valid. {askAgain}";

        if (otpRequest.ExpiresAt < now)
            return $"This code has expired. {askAgain}";

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

            return state.IsUsed
                ? $"This code is no longer valid. {askAgain}"
                : state.ExpiresAt < now
                    ? $"This code has expired. {askAgain}"
                    : $"Too many wrong codes. {askAgain}";
        }

        if (!CodesMatch(otpRequest, entered))
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
                return WaitMessage(SecondsUntil(lockedUntil.Value));

            var attemptsUsed = await _dbContext.OtpRequests
                .AsNoTracking()
                .Where(o => o.Id == otpRequest.Id)
                .Select(o => o.FailedAttempts)
                .FirstAsync();

            var left = maxAttempts - attemptsUsed;

            return left > 0
                ? $"That code is not correct. You have {left} {(left == 1 ? "try" : "tries")} left."
                : $"Too many wrong codes. {askAgain}";
        }

        // Single use: only one request can flip IsUsed from false to true.
        var consumed = await _dbContext.OtpRequests
            .Where(o => o.Id == otpRequest.Id && !o.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));

        if (consumed != 1)
            return $"This code is no longer valid. {askAgain}";

        // In one statement too: the counters were changed outside change tracking above.
        await _dbContext.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginAttempts, 0)
                .SetProperty(u => u.LockedUntilUtc, (DateTime?)null));

        return null;
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

        if (await UseCodeAsync(user, OtpRequest.SignInPurpose, request.OtpCode, "Go back and ask for a new code.") is { } failure)
            return Fail(failure);

        var token = await _tokenService.GenerateTokenAsync(user);

        await _dbContext.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, DateTime.UtcNow));

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

        // Sign-in alert when this browser hasn't been used for this account before.
        var knownDeviceToken = await NoteKnownDeviceAsync(user, request.KnownDeviceToken, request.DeviceName);

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
            DeviceExpiresAt = deviceExpiresAt,
            KnownDeviceToken = knownDeviceToken
        };
    }

    /// <summary>
    /// Sign-in alerts (owner, 2026-10-15): records the browser for this account and, if the
    /// account has signed in elsewhere before but never from this browser, tells the person
    /// by SMS. The first browser an account ever uses is recorded quietly. Returns a new
    /// browser key when the browser had none (it goes only into an HttpOnly cookie).
    /// </summary>
    private async Task<string?> NoteKnownDeviceAsync(User user, string? key, string? deviceName)
    {
        string? newKey = null;

        if (string.IsNullOrWhiteSpace(key) || key.Length != 64 || !key.All(Uri.IsHexDigit))
            key = newKey = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        var hash = _codeHasher.HashDeviceToken(key);
        var now = DateTime.UtcNow;
        var name = string.IsNullOrWhiteSpace(deviceName) ? "a web browser" : deviceName.Trim();
        if (name.Length > 100) name = name[..100];

        var known = await _dbContext.KnownDevices
            .FirstOrDefaultAsync(d => d.UserId == user.Id && d.KeyHash == hash);

        if (known != null)
        {
            known.LastSeenAt = now;
            await _dbContext.SaveChangesAsync();
            return newKey;
        }

        var others = await _dbContext.KnownDevices
            .Where(d => d.UserId == user.Id)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync();

        _dbContext.KnownDevices.RemoveRange(others.Skip(MaxKnownDevices - 1));
        _dbContext.KnownDevices.Add(new KnownDevice
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            KeyHash = hash,
            Name = name,
            FirstSeenAt = now,
            LastSeenAt = now
        });
        await _dbContext.SaveChangesAsync();

        if (others.Count == 0)
        {
            await _auditService.LogAsync(user.Id, $"First sign-in recorded on {name}", "User", user.Id.ToString());
            return newKey;
        }

        var sent = await _smsSender.SendAsync(user.MobileNumber,
            $"{(await _settingsService.GetBrandingAsync()).ShortName}: new sign-in to your account at " +
            $"{HR28.Application.Common.MaldivesTime.Now:HH:mm} (Maldives time) on {name}. " +
            "If this wasn't you, contact your administrator now.");

        await _auditService.LogAsync(user.Id,
            $"Signed in from a new device: {name}" + (sent ? "; alert SMS sent" : "; alert SMS could not be sent"),
            "User", user.Id.ToString());

        return newKey;
    }

    public async Task<StepUpSendResultDto> SendStepUpCodeAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        if (user == null)
            return new StepUpSendResultDto { Message = "Please sign in again." };

        if (await CodeWaitSecondsAsync(user) is { } wait)
            return new StepUpSendResultDto { IsThrottled = true, RetryAfterSeconds = wait, Message = WaitMessage(wait), MaskedMobile = Masked(user.MobileNumber) };

        var sent = await IssueCodeAsync(user, OtpRequest.StepUpPurpose, async (otp, minutes) =>
            $"{(await _settingsService.GetBrandingAsync()).ShortName} confirmation code: {otp}. " +
            "Use it only to confirm a change you are making right now. Never share it.");

        if (!sent)
            return new StepUpSendResultDto { Message = "We couldn't send your code right now. Please try again later.", MaskedMobile = Masked(user.MobileNumber) };

        return new StepUpSendResultDto
        {
            Success = true,
            MaskedMobile = Masked(user.MobileNumber),
            ExpiresInSeconds = (await _settingsService.GetAsync()).OtpExpiryMinutes * 60
        };
    }

    public async Task<StepUpVerifyResultDto> VerifyStepUpAsync(Guid userId, string? code, DateTime signedInAtUtc)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        if (user == null)
            return new StepUpVerifyResultDto { Message = "Please sign in again." };

        if (await UseCodeAsync(user, OtpRequest.StepUpPurpose, code, "Please ask for a new code.") is { } failure)
            return new StepUpVerifyResultDto { Message = failure };

        await _auditService.LogAsync(user.Id, "Confirmed it's them for sensitive actions (step-up)", "User", user.Id.ToString());

        return new StepUpVerifyResultDto
        {
            Success = true,
            Token = await _tokenService.GenerateTokenAsync(user, signedInAtUtc, DateTime.UtcNow),
            ValidForSeconds = (int)HR28.Application.Common.StepUpRules.ValidFor.TotalSeconds
        };
    }
}
