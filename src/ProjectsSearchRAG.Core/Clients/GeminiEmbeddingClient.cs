using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using Pgvector;
using ProjectsSearchRAG.Core.Clients.IClients;
using ProjectsSearchRAG.Core.Options;

namespace ProjectsSearchRAG.Core.Clients
{
    public class GeminiEmbeddingClient : IGeminiEmbeddingClient
    {
        private const string RetrievalQueryTaskType = "RETRIEVAL_QUERY";
        private const string RetrievalDocumentTaskType = "RETRIEVAL_DOCUMENT";

        private readonly GeminiOptions _options;
        private Client? _client;

        public GeminiEmbeddingClient(IOptions<GeminiOptions> options)
        {
            _options = options.Value;
        }

        private Client Client => _client ??= new Client(apiKey: _options.ApiKey);

        public async Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var response = await Client.Models.EmbedContentAsync(
                model: _options.EmbeddingModel,
                contents: text,
                config: BuildConfig(RetrievalQueryTaskType),
                cancellationToken: cancellationToken);

            var embeddings = response.Embeddings
                ?? throw new InvalidOperationException("Gemini didn't return valid embeddings");
            var embedding = embeddings.FirstOrDefault()
                ?? throw new InvalidOperationException("Gemini didn't return valid embeddings");

            var values = embedding.Values?.Select(v => (float)v).ToArray()
                ?? throw new InvalidOperationException("Gemini didn't return valid embeddings");

            return new Vector(values);
        }

        public async Task<IReadOnlyList<Vector>> GenerateBatchEmbeddingsAsync(
            IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (texts.Count == 0) return [];

            var contents = texts
                .Select(t => new Content { Parts = new List<Part> { new Part { Text = t } } })
                .ToList();

            var response = await Client.Models.EmbedContentAsync(
                model: _options.EmbeddingModel,
                contents: contents,
                config: BuildConfig(RetrievalDocumentTaskType),
                cancellationToken: cancellationToken);

            var embeddings = response.Embeddings
                ?? throw new InvalidOperationException("Gemini didn't return valid embeddings");

            return embeddings
                .Select(e => new Vector(
                    e.Values?.Select(v => (float)v).ToArray()
                    ?? throw new InvalidOperationException("Gemini didn't return valid embeddings")))
                .ToList();
        }

        private EmbedContentConfig BuildConfig(string taskType) => new()
        {
            TaskType = taskType,
            OutputDimensionality = _options.EmbeddingDimension
        };
    }
}