using Pgvector;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectsSearchRAG.Core.Clients.IClients;

public interface IGeminiEmbeddingClient
{
    Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vector>> GenerateBatchEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
