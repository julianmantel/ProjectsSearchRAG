using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using ProjectsSearchRAG.Data.Context;
using ProjectsSearchRAG.Data.Models;
using ProjectsSearchRAG.Data.Repositories.IRepository;

namespace ProjectsSearchRAG.Data.Repositories;

public class CodeChunkRepository : ICodeChunkRepository
{
    private readonly AppDbContext _context;

    public CodeChunkRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task InsertChunksAsync(Guid projectId, IEnumerable<CodeChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _context.CodeChunks.AddRangeAsync(chunks, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var existingChunks = _context.CodeChunks.Where(c => c.ProjectId == projectId);
        _context.CodeChunks.RemoveRange(existingChunks);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CodeChunk>> SearchSimilarChunksAsync(
        Guid projectId, 
        Vector queryEmbedding, 
        int topK = 5, 
        CancellationToken cancellationToken = default)
    {
        return await _context.CodeChunks
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId && c.Embedding != null)
            .OrderBy(c => c.Embedding!.CosineDistance(queryEmbedding))
            .Take(topK)
            .ToListAsync(cancellationToken);
    }
}
