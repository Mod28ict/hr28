using HR28.Domain.Entities;

namespace HR28.Application.Interfaces;

public interface ITokenService
{
    Task<string> GenerateTokenAsync(User user);
}