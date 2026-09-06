using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectsSearchRAG.Core.Clients.IClients;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Core.Services.IService;
using ProjectsSearchRAG.Data.Context;
using ProjectsSearchRAG.Data.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectsSearchRAG.Core.Services
{
    public class CodeIndexerService : ICodeIndexerService
    {
        private readonly ICodeChunkerService _chunker;
        private readonly IGeminiEmbeddingClient _embeddingClient;
        private readonly AppDbContext _db;
        private readonly GeminiOptions _geminiOptions;
        private readonly IndexerOptions _indexerOptions;
        private readonly ILogger<CodeIndexerService> _logger;

        public CodeIndexerService(
            ICodeChunkerService chunker,
            IGeminiEmbeddingClient embeddingClient,
            AppDbContext db,
            IOptions<GeminiOptions> geminiOptions,
            IOptions<IndexerOptions> indexerOptions,
            ILogger<CodeIndexerService> logger)
        {
            _chunker = chunker;
            _embeddingClient = embeddingClient;
            _db = db;
            _geminiOptions = geminiOptions.Value;
            _indexerOptions = indexerOptions.Value;
            _logger = logger;
        }

        public async Task<int> IndexProjectAsync(Project project, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report($"Chunking '{project.Name}'...");

            var existingProject = await _db.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == project.Id || p.Path == project.Path, cancellationToken);

            if (existingProject is null)
            {
                _db.Projects.Add(project);
                await _db.SaveChangesAsync(cancellationToken);
            }
            else if (_db.Projects.Entry(project).State == EntityState.Detached)
            {
                project.Id = existingProject.Id;
                _db.Projects.Attach(project);
            }

            var chunks = _chunker.ChunkProject(project.Path, project.Id, _indexerOptions.SupportedExtensions);
            if (chunks.Count == 0)
            {
                progress?.Report("Don't found any files to index.");
                return 0;
            }

            progress?.Report($"Find {chunks.Count} fragments. Generating embeddings...");

            var batches = chunks.Chunk(_indexerOptions.EmbeddingBatchSize).ToList();
            var maxConcurrency = Math.Max(1, _geminiOptions.MaxConcurrentEmbeddings);
            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var processedBatches = 0;

            var batchTasks = batches.Select(async batch =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var texts = batch.Select(c => c.Content).ToList();
                    var vectors = await _embeddingClient.GenerateBatchEmbeddingsAsync(texts, cancellationToken);

                    for (var i = 0; i < batch.Length; i++)
                        batch[i].Embedding = vectors[i];

                    var done = Interlocked.Increment(ref processedBatches);
                    progress?.Report($"Lote {done}/{batches.Count} vectorizado ({batch.Length} chunks).");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex,
                        "The generation of embeddings failed for a batch of {Count} chunks from {Project}",
                        batch.Length, project.Name);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(batchTasks);

            var readyChunks = chunks.Where(c => c.Embedding is not null).ToList();

            progress?.Report("Saving to the database...");

            var previousChunks = _db.CodeChunks.Where(c => c.ProjectId == project.Id);
            _db.CodeChunks.RemoveRange(previousChunks);

            await _db.CodeChunks.AddRangeAsync(readyChunks, cancellationToken);

            project.IndexedAt = DateTime.UtcNow;
            _db.Projects.Update(project);

            await _db.SaveChangesAsync(cancellationToken);

            progress?.Report($"Completed: {readyChunks.Count}/{chunks.Count} indexed fragments.");

            return readyChunks.Count;
        }
    }

}
