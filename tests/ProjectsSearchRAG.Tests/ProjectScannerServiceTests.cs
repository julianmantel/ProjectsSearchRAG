using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Core.Services;
using ProjectsSearchRAG.Data.Models;
using ProjectsSearchRAG.Data.Repositories.IRepository;
using Xunit;

namespace ProjectsSearchRAG.Tests;

public class ProjectScannerServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly Mock<IProjectRepository> _mockRepo;
    private readonly IOptions<IndexerOptions> _indexerOptions;

    public ProjectScannerServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ProjectsRAG_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        _mockRepo = new Mock<IProjectRepository>();
        _indexerOptions = Microsoft.Extensions.Options.Options.Create(new IndexerOptions
        {
            DefaultSourcePath = _tempDirectory
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Fact]
    public async Task DiscoverProjectsAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        // Arrange
        var service = new ProjectScannerService(
            _mockRepo.Object,
            _indexerOptions,
            NullLogger<ProjectScannerService>.Instance);

        var nonExistentPath = Path.Combine(_tempDirectory, "does_not_exist_12345");

        // Act
        var result = await service.DiscoverProjectsAsync(nonExistentPath);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoverProjectsAsync_FindsFirstLevelDirectories_IgnoresHiddenAndSpecialFolders()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "ProjectAlpha"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "ProjectBeta"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, ".git"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "node_modules"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "bin"));

        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project>());

        var service = new ProjectScannerService(
            _mockRepo.Object,
            _indexerOptions,
            NullLogger<ProjectScannerService>.Instance);

        // Act
        var result = await service.DiscoverProjectsAsync(_tempDirectory);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "ProjectAlpha" && !p.IsIndexed);
        Assert.Contains(result, p => p.Name == "ProjectBeta" && !p.IsIndexed);
    }

    [Fact]
    public async Task DiscoverProjectsAsync_CrossReferencesWithDatabase_MarksIndexedCorrectly()
    {
        // Arrange
        var dirAlpha = Directory.CreateDirectory(Path.Combine(_tempDirectory, "ProjectAlpha"));
        var dirBeta = Directory.CreateDirectory(Path.Combine(_tempDirectory, "ProjectBeta"));

        var indexedDate = DateTime.UtcNow.AddDays(-1);
        var existingProjectInDb = new Project
        {
            Id = Guid.NewGuid(),
            Name = "ProjectAlpha",
            Path = dirAlpha.FullName,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            IndexedAt = indexedDate
        };

        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { existingProjectInDb });

        var service = new ProjectScannerService(
            _mockRepo.Object,
            _indexerOptions,
            NullLogger<ProjectScannerService>.Instance);

        // Act
        var result = await service.DiscoverProjectsAsync(_tempDirectory);

        // Assert
        Assert.Equal(2, result.Count);

        var alpha = result.First(p => p.Name == "ProjectAlpha");
        Assert.True(alpha.IsIndexed);
        Assert.Equal(indexedDate, alpha.IndexedAt);
        Assert.Equal(existingProjectInDb.Id, alpha.Id);

        var beta = result.First(p => p.Name == "ProjectBeta");
        Assert.False(beta.IsIndexed);
        Assert.Null(beta.IndexedAt);
    }
}
