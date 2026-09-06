using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Clients.IClients;

public interface IGeminiChatClient
{
    Task<string> GenerateAnswerAsync(string userQuestion, IReadOnlyList<CodeChunk> contextChunks, string selectedChatModel, CancellationToken cancellationToken = default);
}
