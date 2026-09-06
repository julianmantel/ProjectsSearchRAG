using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Core.Services.Chunking;
using ProjectsSearchRAG.Core.Services.IService;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Core.Services;

public class CodeChunkerService : ICodeChunkerService
{
    private readonly IndexerOptions _indexerOptions;
    private readonly ILogger<CodeChunkerService> _logger;

    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".vscode", ".idea", "node_modules", "dist", "build", "coverage", ".next"
    };

    public CodeChunkerService(
        IOptions<IndexerOptions> indexerOptions,
        ILogger<CodeChunkerService>? logger = null)
    {
        _indexerOptions = indexerOptions.Value;
        _logger = logger ?? NullLogger<CodeChunkerService>.Instance;
    }

    public IReadOnlyList<CodeChunk> ChunkFile(string relativeFilePath, string fileContent, Guid projectId = default)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return Array.Empty<CodeChunk>();
        }

        var ext = Path.GetExtension(relativeFilePath).ToLowerInvariant();

        return ext switch
        {
            ".cs" => CSharpChunker.Chunk(relativeFilePath, fileContent, projectId),
            ".tsx" or ".ts" or ".jsx" or ".js" => TsxChunker.Chunk(relativeFilePath, fileContent, projectId),
            ".sql" => SqlChunker.Chunk(relativeFilePath, fileContent, projectId),
            ".json" => JsonChunker.Chunk(relativeFilePath, fileContent, projectId),
            _ => CSharpChunker.ChunkByLineBlocks(relativeFilePath, fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None), "text", null, projectId)
        };
    }

    public IReadOnlyList<CodeChunk> ChunkProject(string projectPath, Guid projectId = default, IReadOnlyList<string>? supportedExtensions = null)
    {
        var result = new List<CodeChunk>();

        if (!Directory.Exists(projectPath))
        {
            _logger.LogWarning("Project path does not exist: {Path}", projectPath);
            return result;
        }

        var extensions = supportedExtensions ?? _indexerOptions.SupportedExtensions;
        var extSet = new HashSet<string>(extensions.Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

        var files = GetFilesRecursively(projectPath, extSet);
        _logger.LogInformation("Found {Count} candidate file(s) for chunking in '{Path}'", files.Count, projectPath);

        foreach (var file in files)
        {
            try
            {
                var relativePath = Path.GetRelativePath(projectPath, file).Replace('\\', '/');
                var content = File.ReadAllText(file);
                var fileChunks = ChunkFile(relativePath, content, projectId);
                result.AddRange(fileChunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading and chunking file: {FilePath}", file);
            }
        }

        return result;
    }

    private List<string> GetFilesRecursively(string directory, HashSet<string> extensions)
    {
        var filesList = new List<string>();

        try
        {
            var dirInfo = new DirectoryInfo(directory);
            if (ExcludedDirs.Contains(dirInfo.Name) || dirInfo.Name.StartsWith('.'))
            {
                return filesList;
            }

            foreach (var file in dirInfo.GetFiles())
            {
                if (extensions.Contains(file.Extension))
                {
                    filesList.Add(file.FullName);
                }
            }

            foreach (var subDir in dirInfo.GetDirectories())
            {
                filesList.AddRange(GetFilesRecursively(subDir.FullName, extensions));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not access directory: {Directory}", directory);
        }

        return filesList;
    }
}
