using Microsoft.Extensions.Logging;
using ProjectsSearchRAG.Core.Clients.IClients;
using ProjectsSearchRAG.Core.Models;
using ProjectsSearchRAG.Core.Services.IService;
using ProjectsSearchRAG.Data.Repositories.IRepository;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectsSearchRAG.Core.Services
{
    public class RagSearchService : IRagSearchService
    {
        private static readonly int DefaultTopK = 5;

        private readonly IGeminiEmbeddingClient _embeddingClient;
        private readonly IGeminiChatClient _chatClient;
        private readonly ICodeChunkRepository _codeChunkRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ILogger<RagSearchService> _logger;

        public RagSearchService(
            IGeminiEmbeddingClient embeddingClient,
            IGeminiChatClient chatClient,
            ICodeChunkRepository codeChunkRepository,
            IProjectRepository projectRepository,
            ILogger<RagSearchService> logger)
        {
            _embeddingClient = embeddingClient;
            _chatClient = chatClient;
            _codeChunkRepository = codeChunkRepository;
            _projectRepository = projectRepository;
            _logger = logger;
        }

        public async Task<RagSearchResult> AskQuestionAsync(
            Guid projectId, string question, string selectedChatModel,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                throw new ArgumentException("The question cannot be empty.", nameof(question));
            }

            var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
            if (project is null)
            {
                throw new InvalidOperationException($"The project with id '{projectId}' does not exist.");
            }
            if (!project.IsIndexed)
            {
                throw new InvalidOperationException($"The project '{project.Name}' is not indexed.");
            }

            _logger.LogInformation("Generating embedding for the question for '{Project}'...", project.Name);
            var questionEmbedding = await _embeddingClient.GenerateEmbeddingAsync(question, cancellationToken);

            var chunks = await _codeChunkRepository.SearchSimilarChunksAsync(
                projectId, questionEmbedding, DefaultTopK, cancellationToken);

            if (chunks.Count == 0)
            {
                _logger.LogInformation("No relevant fragments found for '{Project}'.", project.Name);
                return new RagSearchResult
                {
                    Answer = "No relevant fragments found in the indexed project to answer the question.",
                    CitedChunks = []
                };
            }

            _logger.LogInformation("Generating answer with {ChunkCount} fragments as context for '{Project}'.",
                chunks.Count, project.Name);
            var answer = await _chatClient.GenerateAnswerAsync(question, chunks, selectedChatModel, cancellationToken);

            return new RagSearchResult
            {
                Answer = answer,
                CitedChunks = chunks
            };
        }
    }
}