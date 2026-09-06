using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Services.IService;

public interface IProjectScannerService
{
    Task<IReadOnlyList<Project>> DiscoverProjectsAsync(string? rootDirectory = null, CancellationToken cancellationToken = default);
}
