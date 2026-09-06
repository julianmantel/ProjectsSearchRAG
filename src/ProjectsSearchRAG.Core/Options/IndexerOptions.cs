using System.Collections.Generic;

namespace ProjectsSearchRAG.Core.Options;

public class IndexerOptions
{
    public const string SectionName = "Indexer";
    public string DefaultSourcePath { get; set; } = "/source";
    public List<string> SupportedExtensions { get; set; } = [".cs", ".tsx", ".ts", ".js", ".sql", ".json"];
    public int EmbeddingBatchSize { get; set; } = 50;
}
