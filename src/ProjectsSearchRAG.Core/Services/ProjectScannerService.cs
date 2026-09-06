using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Core.Services.IService;
using ProjectsSearchRAG.Data.Models;
using ProjectsSearchRAG.Data.Repositories.IRepository;

namespace ProjectsSearchRAG.Core.Services;

public class ProjectScannerService : IProjectScannerService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IndexerOptions _indexerOptions;
    private readonly ILogger<ProjectScannerService> _logger;

    private static readonly HashSet<string> IgnoredFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".idea",
        ".vscode",
        "node_modules",
        "bin",
        "obj",
        "$RECYCLE.BIN",
        "System Volume Information"
    };

    public ProjectScannerService(
        IProjectRepository projectRepository,
        IOptions<IndexerOptions> indexerOptions,
        ILogger<ProjectScannerService> logger)
    {
        _projectRepository = projectRepository;
        _indexerOptions = indexerOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Project>> DiscoverProjectsAsync(string? rootDirectory = null, CancellationToken cancellationToken = default)
    {
        var targetPath = string.IsNullOrWhiteSpace(rootDirectory) ? _indexerOptions.DefaultSourcePath : rootDirectory;

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            _logger.LogWarning("No root directory was provided and default path is empty.");
            return Array.Empty<Project>();
        }

        targetPath = Environment.ExpandEnvironmentVariables(targetPath);
        if (!Directory.Exists(targetPath))
        {
            _logger.LogWarning("Directory does not exist: {Path}", targetPath);
            return Array.Empty<Project>();
        }

        _logger.LogInformation("Scanning first-level directories in: {Path}", targetPath);

        // 1. Obtener los subdirectorios del directorio raíz
        DirectoryInfo rootDirInfo = new(targetPath);
        DirectoryInfo[] subDirectories;
        try
        {
            subDirectories = rootDirInfo.GetDirectories();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read subdirectories from: {Path}", targetPath);
            return Array.Empty<Project>();
        }

        // Se filtran los subdirectorios ocultos o que no sean relevantes (como .git, node_modules, etc)
        var candidateDirs = subDirectories
            .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden) &&
                        !d.Name.StartsWith('.') &&
                        !IgnoredFolderNames.Contains(d.Name))
            .ToList();

        // 2. Ver cuales de esos proyectos ya están en la base de datos
        IReadOnlyList<Project> dbProjects;
        try
        {
            dbProjects = await _projectRepository.GetAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch projects from database. Proceeding with filesystem-only data.");
            dbProjects = Array.Empty<Project>();
        }

        // Crear diccionarios para búsqueda rápida por Path y Name, ej: Proyecto 1 -> C:\Projects\Project1
        var dbByPath = dbProjects.ToDictionary(
            p => NormalizePath(p.Path),
            p => p,
            StringComparer.OrdinalIgnoreCase);

        var dbByName = dbProjects.ToDictionary(
            p => p.Name,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        var result = new List<Project>();

        foreach (var dir in candidateDirs)
        {
            var normalizedDirPath = NormalizePath(dir.FullName);
            var folderName = dir.Name;

            if (dbByPath.TryGetValue(normalizedDirPath, out var matchedProject))
            {
                result.Add(matchedProject);
            }
            else if (dbByName.TryGetValue(folderName, out var matchedByName))
            {
                result.Add(matchedByName);
            }
            else
            {
                result.Add(new Project
                {
                    Id = Guid.NewGuid(),
                    Name = folderName,
                    Path = dir.FullName,
                    CreatedAt = dir.CreationTimeUtc,
                    IndexedAt = null
                });
            }
        }

        return result.OrderBy(p => p.Name).ToList();
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
