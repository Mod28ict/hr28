using HR28.Application.DTOs.Access;

namespace HR28.Application.Interfaces;

public interface IAccessScopeService
{
    Task<AccessScope> GetAsync(Guid userId);
}
