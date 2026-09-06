using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Services.Chunking;

public static class TsxChunker
{
    private static readonly Regex FunctionOrComponentRegex = new(
        @"(?:export\s+(?:default\s+)?)?(?:async\s+)?function\s+([A-Za-z0-9_]+)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex ArrowFunctionRegex = new(
        @"(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_]+)\s*=\s*(?:async\s*)?(?:\([^)]*\)|[A-Za-z0-9_]+)\s*=>",
        RegexOptions.Compiled);

    private static readonly Regex ClassRegex = new(
        @"(?:export\s+(?:default\s+)?)?class\s+([A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    public static List<CodeChunk> Chunk(string filePath, string content, Guid projectId)
    {
        var chunks = new List<CodeChunk>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var lang = filePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ? "typescriptreact" :
                   filePath.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ? "javascriptreact" :
                   filePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ? "typescript" : "javascript";

        int i = 0;
        string? currentClass = null;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            var classMatch = ClassRegex.Match(trimmed);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
            }

            var fnMatch = FunctionOrComponentRegex.Match(trimmed);
            var arrowMatch = ArrowFunctionRegex.Match(trimmed);

            string? detectedName = null;
            if (fnMatch.Success)
            {
                detectedName = fnMatch.Groups[1].Value;
            }
            else if (arrowMatch.Success)
            {
                detectedName = arrowMatch.Groups[1].Value;
            }

            if (detectedName != null)
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
                        bool isComponent = char.IsUpper(detectedName[0]);

                        chunks.Add(new CodeChunk
                        {
                            Id = Guid.NewGuid(),
                            ProjectId = projectId,
                            FilePath = filePath,
                            Language = lang,
                            ClassName = isComponent ? detectedName : currentClass,
                            MethodName = isComponent ? null : detectedName,
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
                    Language = lang,
                    ClassName = currentClass,
                    MethodName = null,
                    StartLine = 1,
                    EndLine = lines.Length,
                    Content = content.Trim()
                });
            }
            else
            {
                chunks.AddRange(CSharpChunker.ChunkByLineBlocks(filePath, lines, lang, currentClass, projectId));
            }
        }

        return chunks;
    }

    private static int FindBlockEnd(string[] lines, int startIndex)
    {
        int parenCount = 0;
        bool passedParams = false;
        int braceCount = 0;
        bool foundBodyOpeningBrace = false;

        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];

            for (int charIdx = 0; charIdx < line.Length; charIdx++)
            {
                char c = line[charIdx];

                if (!passedParams)
                {
                    if (c == '(') { parenCount++; }
                    else if (c == ')')
                    {
                        parenCount--;
                        if (parenCount == 0)
                        {
                            passedParams = true;
                        }
                    }
                    continue;
                }

                if (c == '{')
                {
                    braceCount++;
                    foundBodyOpeningBrace = true;
                }
                else if (c == '}')
                {
                    braceCount--;
                    if (foundBodyOpeningBrace && braceCount == 0)
                    {
                        return i + 1;
                    }
                }
            }

            if (passedParams && !foundBodyOpeningBrace && line.Contains("=>") && line.TrimEnd().EndsWith(';'))
            {
                return i + 1;
            }
        }

        return -1;
    }
}
