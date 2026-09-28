using HR28.Application.DTOs.Islands;

namespace HR28.Application.Interfaces;

public interface IIslandService
{
    Task<IEnumerable<IslandDto>> GetAllAsync();
    Task<IslandDto?> GetByIdAsync(Guid id);
    Task<IslandDto> CreateAsync(CreateIslandDto dto);
    Task<bool> UpdateAsync(Guid id, UpdateIslandDto dto);
    Task<bool> DeleteAsync(Guid id);
}