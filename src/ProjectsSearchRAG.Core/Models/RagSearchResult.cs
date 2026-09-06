using System.Collections.Generic;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Models;

public class RagSearchResult
{
    public string Answer { get; set; } = string.Empty;
    public IReadOnlyList<CodeChunk> CitedChunks { get; set; } = [];
}
