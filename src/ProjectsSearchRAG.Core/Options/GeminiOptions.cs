namespace ProjectsSearchRAG.Core.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimension { get; set; }
    public string ChatModel { get; set; } = string.Empty;
    public int MaxConcurrentEmbeddings { get; set; } = 3;
}
