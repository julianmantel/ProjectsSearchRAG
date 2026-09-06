using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Services.Chunking;

public static class JsonChunker
{
    private static readonly Regex RootPropertyRegex = new(
        @"^\s*""([A-Za-z0-9_\-\.]+)""\s*:\s*([\{\[])?",
        RegexOptions.Compiled);

    public static List<CodeChunk> Chunk(string filePath, string content, Guid projectId)
    {
        var chunks = new List<CodeChunk>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var match = RootPropertyRegex.Match(line);

            if (match.Success)
            {
                string propertyName = match.Groups[1].Value;
                bool isBlock = match.Groups[2].Success;

                int startLine = i + 1;
                int endLine = isBlock ? FindJsonBlockEnd(lines, i) : startLine;

                if (endLine >= startLine)
                {
                    var chunkLines = new List<string>();
                    for (int l = startLine - 1; l < endLine; l++)
                    {
                        chunkLines.Add(lines[l]);
                    }

                    var chunkContent = string.Join(Environment.NewLine, chunkLines).Trim();
                    if (!string.IsNullOrWhiteSpace(chunkContent))
                    {
                        chunks.Add(new CodeChunk
                        {
                            Id = Guid.NewGuid(),
                            ProjectId = projectId,
                            FilePath = filePath,
                            Language = "json",
                            ClassName = propertyName,
                            MethodName = null,
                            StartLine = startLine,
                            EndLine = endLine,
                            Content = chunkContent
                        });
                    }

                    i = endLine;
                    continue;
                }
            }

            i++;
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
                    Language = "json",
                    ClassName = null,
                    MethodName = null,
                    StartLine = 1,
                    EndLine = lines.Length,
                    Content = content.Trim()
                });
            }
            else
            {
                chunks.AddRange(CSharpChunker.ChunkByLineBlocks(filePath, lines, "json", null, projectId));
            }
        }

        return chunks;
    }

    private static int FindJsonBlockEnd(string[] lines, int startIndex)
    {
        int braceCount = 0;
        int bracketCount = 0;
        bool found = false;

        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (char c in line)
            {
                if (c == '{') { braceCount++; found = true; }
                else if (c == '}') { braceCount--; }
                else if (c == '[') { bracketCount++; found = true; }
                else if (c == ']') { bracketCount--; }

                if (found && braceCount == 0 && bracketCount == 0)
                {
                    return i + 1;
                }
            }
        }

        return -1;
    }
}
