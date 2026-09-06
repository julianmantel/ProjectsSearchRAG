using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Services.Chunking;

public static class CSharpChunker
{
    private static readonly Regex ClassOrInterfaceRegex = new(
        @"\b(public|internal|private|protected)?\s*(abstract|sealed|static|partial)?\s*\b(class|interface|struct|record)\s+([A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex MethodRegex = new(
        @"(?:(?:public|private|protected|internal|static|async|virtual|override|abstract|sealed)\s+)+([A-Za-z0-9_<>,\[\]\?]+)\s+([A-Za-z0-9_]+)\s*\(([^)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex ConstructorRegex = new(
        @"(?:(?:public|private|protected|internal)\s+)+([A-Za-z0-9_]+)\s*\(([^)]*)\)",
        RegexOptions.Compiled);

    public static List<CodeChunk> Chunk(string filePath, string content, Guid projectId)
    {
        var chunks = new List<CodeChunk>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        string? currentClass = null;
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Detect class/interface/record/struct
            var classMatch = ClassOrInterfaceRegex.Match(trimmed);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[4].Value;
            }

            // Detect method or constructor
            var methodMatch = MethodRegex.Match(trimmed);
            var ctorMatch = ConstructorRegex.Match(trimmed);

            string? detectedMethodName = null;
            if (methodMatch.Success && !trimmed.StartsWith("class ") && !trimmed.StartsWith("return "))
            {
                detectedMethodName = methodMatch.Groups[2].Value;
            }
            else if (ctorMatch.Success && currentClass != null && ctorMatch.Groups[1].Value == currentClass)
            {
                detectedMethodName = ctorMatch.Groups[1].Value + " (Constructor)";
            }

            if (detectedMethodName != null)
            {
                int startLine = i + 1;
                int endLine = FindBlockEnd(lines, i);

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
                            Language = "csharp",
                            ClassName = currentClass,
                            MethodName = detectedMethodName,
                            StartLine = startLine,
                            EndLine = endLine,
                            Content = chunkContent
                        });
                    }

                    i = endLine; // advance past the method
                    continue;
                }
            }

            i++;
        }

        // If no methods were extracted (e.g. DTOs, interfaces, or scripts), chunk by sliding block or whole file
        if (chunks.Count == 0)
        {
            if (lines.Length <= 40)
            {
                var className = currentClass ?? ExtractFirstClassName(lines);
                chunks.Add(new CodeChunk
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    FilePath = filePath,
                    Language = "csharp",
                    ClassName = className,
                    MethodName = null,
                    StartLine = 1,
                    EndLine = lines.Length,
                    Content = content.Trim()
                });
            }
            else
            {
                chunks.AddRange(ChunkByLineBlocks(filePath, lines, "csharp", currentClass, projectId));
            }
        }

        return chunks;
    }

    private static int FindBlockEnd(string[] lines, int startIndex)
    {
        int braceCount = 0;
        bool foundOpeningBrace = false;

        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (char c in line)
            {
                if (c == '{')
                {
                    braceCount++;
                    foundOpeningBrace = true;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (foundOpeningBrace && braceCount == 0)
                    {
                        return i + 1; // 1-based index
                    }
                }
            }

            // Arrow expression body single-line method: "=> something;"
            if (!foundOpeningBrace && line.Contains("=>") && line.TrimEnd().EndsWith(';'))
            {
                return i + 1;
            }
        }

        return -1;
    }

    private static string? ExtractFirstClassName(string[] lines)
    {
        foreach (var line in lines)
        {
            var match = ClassOrInterfaceRegex.Match(line.Trim());
            if (match.Success) return match.Groups[4].Value;
        }
        return null;
    }

    public static List<CodeChunk> ChunkByLineBlocks(string filePath, string[] lines, string language, string? className, Guid projectId, int blockSize = 60, int overlap = 10)
    {
        var chunks = new List<CodeChunk>();
        int totalLines = lines.Length;
        int start = 0;

        while (start < totalLines)
        {
            int end = Math.Min(start + blockSize, totalLines);
            var blockLines = new List<string>();
            for (int i = start; i < end; i++)
            {
                blockLines.Add(lines[i]);
            }

            var chunkContent = string.Join(Environment.NewLine, blockLines).Trim();
            if (!string.IsNullOrWhiteSpace(chunkContent))
            {
                chunks.Add(new CodeChunk
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    FilePath = filePath,
                    Language = language,
                    ClassName = className,
                    MethodName = null,
                    StartLine = start + 1,
                    EndLine = end,
                    Content = chunkContent
                });
            }

            if (end == totalLines) break;
            start += (blockSize - overlap);
        }

        return chunks;
    }
}
