using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Services.IService;

public interface ICodeIndexerService
{
    Task<int> IndexProjectAsync(Project project, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
