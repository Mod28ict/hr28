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

    public AuthService(
        HR28DbContext dbContext,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<bool> GenerateOtpAsync(
        GenerateOtpRequestDto request)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.AuthorizationCode == request.AuthorizationCode);

        if (user == null)
            return false;

        var otp = OtpGenerator.Generate();

        var otpRequest = new OtpRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OtpCode = otp,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            FailedAttempts = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.OtpRequests.Add(otpRequest);

        await _dbContext.SaveChangesAsync();

        Console.WriteLine(
            $"OTP for {user.FullName}: {otp}");

        return true;
    }

    public async Task<LoginResponseDto> VerifyOtpAsync(
        VerifyOtpRequestDto request)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.AuthorizationCode == request.AuthorizationCode);

        if (user == null)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "Invalid Authorization Code."
            };
        }

        var otpRequest = await _dbContext.OtpRequests
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otpRequest == null)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "No OTP found."
            };
        }

        if (otpRequest.IsUsed)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "OTP already used."
            };
        }

        if (otpRequest.ExpiresAt < DateTime.UtcNow)
        {
            return new LoginResponseDto
            {
                Success = false,
                Message = "OTP expired."
            };
        }

        if (otpRequest.OtpCode != request.OtpCode)
        {
            otpRequest.FailedAttempts++;

            await _dbContext.SaveChangesAsync();

            return new LoginResponseDto
            {
                Success = false,
                Message = "Invalid OTP."
            };
        }

        otpRequest.IsUsed = true;

        await _dbContext.SaveChangesAsync();

        var token = await _tokenService.GenerateTokenAsync(user);

        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        var roleName =
    await _dbContext.UserRoles
        .Where(x => x.UserId == user.Id)
        .Select(x => x.Role.Name)
        .FirstOrDefaultAsync()
    ?? string.Empty;


        return new LoginResponseDto
        {
            Success = true,
            Message = "OTP verified successfully.",
            Token = token,

            UserId = user.Id,
            FullName = user.FullName,
            RoleName = roleName
        };
    }
}
