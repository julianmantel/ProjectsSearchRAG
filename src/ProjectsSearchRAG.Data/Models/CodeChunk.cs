using System;
using Pgvector;

namespace ProjectsSearchRAG.Data.Models;

public class CodeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? ClassName { get; set; }
    public string? MethodName { get; set; }
    public int? StartLine { get; set; }
    public int? EndLine { get; set; }
    public string Content { get; set; } = string.Empty;
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
}
