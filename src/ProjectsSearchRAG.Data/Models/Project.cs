using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProjectsSearchRAG.Data.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? IndexedAt { get; set; }

    [NotMapped]
    public bool IsIndexed => IndexedAt.HasValue;

    public ICollection<CodeChunk> CodeChunks { get; set; } = new List<CodeChunk>();
}
