using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectsSearchRAG.Core.Models;

namespace ProjectsSearchRAG.Core.Services.IService;

public interface IRagSearchService
{
    Task<RagSearchResult> AskQuestionAsync(Guid projectId, string question, string selectedChatModel, CancellationToken cancellationToken = default);
}
