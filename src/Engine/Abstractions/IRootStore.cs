using Engine.Models;

namespace Engine.Abstractions;

public interface IRootStore
{
    Task<IReadOnlyList<Root>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Root> AddAsync(string path, CancellationToken cancellationToken = default);
    Task<Root> UpdateAsync(Guid id, string path, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
