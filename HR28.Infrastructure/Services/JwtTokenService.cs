using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HR28.Infrastructure.Services;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly HR28DbContext _dbContext;

    public JwtTokenService(
        IConfiguration configuration,
        HR28DbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public async Task<string> GenerateTokenAsync(User user)
    {
        var roles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("nationalId", user.NationalId),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var keyValue = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is missing.");

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(keyValue));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}