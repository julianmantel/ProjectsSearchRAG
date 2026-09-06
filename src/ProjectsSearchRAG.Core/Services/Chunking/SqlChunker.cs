using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Services.Chunking;

public static class SqlChunker
{
    private static readonly Regex SqlStatementStartRegex = new(
        @"^\s*(CREATE\s+(?:OR\s+REPLACE\s+)?(?:TABLE|VIEW|PROCEDURE|FUNCTION|TRIGGER|INDEX|EXTENSION))\s+([A-Za-z0-9_\.""\[\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AlterTableRegex = new(
        @"^\s*(ALTER\s+TABLE)\s+([A-Za-z0-9_\.""\[\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<CodeChunk> Chunk(string filePath, string content, Guid projectId)
    {
        var chunks = new List<CodeChunk>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int currentStart = -1;
        string? currentType = null;
        string? currentTarget = null;
        var currentChunkLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Skip pure comments or blank lines if not in a block
            if (currentStart == -1 && (trimmed.StartsWith("--") || string.IsNullOrWhiteSpace(trimmed)))
            {
                continue;
            }

            var statementMatch = SqlStatementStartRegex.Match(line);
            var alterMatch = AlterTableRegex.Match(line);

            if (statementMatch.Success || alterMatch.Success)
            {
                // Flush previous chunk if any
                if (currentChunkLines.Count > 0 && currentStart != -1)
                {
                    AddSqlChunk(chunks, filePath, currentChunkLines, currentStart, i, currentType, currentTarget, projectId);
                    currentChunkLines.Clear();
                }

                currentStart = i + 1;
                if (statementMatch.Success)
                {
                    currentType = statementMatch.Groups[1].Value;
                    currentTarget = statementMatch.Groups[2].Value.Trim('"', '[', ']');
                }
                else
                {
                    currentType = alterMatch.Groups[1].Value;
                    currentTarget = alterMatch.Groups[2].Value.Trim('"', '[', ']');
                }
            }
            else if (currentStart == -1)
            {
                currentStart = i + 1;
            }

            currentChunkLines.Add(line);

            // Semicolon at end of line or "GO" triggers block end
            if (trimmed.EndsWith(';') || string.Equals(trimmed, "GO", StringComparison.OrdinalIgnoreCase))
            {
                AddSqlChunk(chunks, filePath, currentChunkLines, currentStart, i + 1, currentType, currentTarget, projectId);
                currentChunkLines.Clear();
                currentStart = -1;
                currentType = null;
                currentTarget = null;
            }
        }

        // Flush remainder
        if (currentChunkLines.Count > 0 && currentStart != -1)
        {
            AddSqlChunk(chunks, filePath, currentChunkLines, currentStart, lines.Length, currentType, currentTarget, projectId);
        }

        if (chunks.Count == 0)
        {
            if (lines.Length <= 40)
            {
                chunks.Add(new CodeChunk
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    FilePath = filePath,
                    Language = "sql",
                    ClassName = null,
                    MethodName = null,
                    StartLine = 1,
                    EndLine = lines.Length,
                    Content = content.Trim()
                });
            }
            else
            {
                chunks.AddRange(CSharpChunker.ChunkByLineBlocks(filePath, lines, "sql", null, projectId));
            }
        }

        return chunks;
    }

    private static void AddSqlChunk(
        List<CodeChunk> chunks, 
        string filePath, 
        List<string> lines, 
        int startLine, 
        int endLine, 
        string? type, 
        string? target, 
        Guid projectId)
    {
        var content = string.Join(Environment.NewLine, lines).Trim();
        if (string.IsNullOrWhiteSpace(content)) return;

        bool isTableOrView = type != null && (type.Contains("TABLE", StringComparison.OrdinalIgnoreCase) || type.Contains("VIEW", StringComparison.OrdinalIgnoreCase));

        chunks.Add(new CodeChunk
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            FilePath = filePath,
            Language = "sql",
            ClassName = isTableOrView ? target : null,
            MethodName = !isTableOrView ? target : null,
            StartLine = startLine,
            EndLine = endLine,
            Content = content
        });
    }
}
