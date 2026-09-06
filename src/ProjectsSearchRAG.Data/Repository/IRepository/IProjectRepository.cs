using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Data.Repositories.IRepository;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Project?> GetByPathAsync(string path, CancellationToken cancellationToken = default);
    Task<Project> UpsertAsync(Project project, CancellationToken cancellationToken = default);
    Task UpdateIndexedAtAsync(Guid projectId, DateTime indexedAt, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
