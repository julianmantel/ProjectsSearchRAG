using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pgvector;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Data.Repositories.IRepository;

public interface ICodeChunkRepository
{
    Task InsertChunksAsync(Guid projectId, IEnumerable<CodeChunk> chunks, CancellationToken cancellationToken = default);
    Task DeleteByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CodeChunk>> SearchSimilarChunksAsync(Guid projectId, Vector queryEmbedding, int topK = 5, CancellationToken cancellationToken = default);
}
