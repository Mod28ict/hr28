using HR28.Application.DTOs;
using HR28.Application.DTOs.Constituencies;

namespace HR28.Application.Interfaces;

public interface IConstituencyService
{
    Task<IEnumerable<ConstituencyDto>> GetAllAsync();
    Task<ConstituencyDto?> GetByIdAsync(Guid id);
    Task<ConstituencyDto> CreateAsync(CreateConstituencyDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateConstituencyDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<List<LookupDto>> GetIslandsByConstituencyAsync(
    Guid constituencyId);
}