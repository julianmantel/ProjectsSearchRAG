using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using ProjectsSearchRAG.App.ViewModels;
using ProjectsSearchRAG.Core.Models;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Core.Services.IService;
using ProjectsSearchRAG.Data.Models;
using Xunit;

namespace ProjectsSearchRAG.Tests;

public class MainViewModelTests
{
    private readonly Mock<IProjectScannerService> _mockScanner;
    private readonly Mock<ICodeIndexerService> _mockIndexer;
    private readonly Mock<IRagSearchService> _mockRagSearch;
    private readonly IOptions<IndexerOptions> _indexerOptions;
    private readonly IOptions<GeminiOptions> _geminiOptions;

    public MainViewModelTests()
    {
        _mockScanner = new Mock<IProjectScannerService>();
        _mockIndexer = new Mock<ICodeIndexerService>();
        _mockRagSearch = new Mock<IRagSearchService>();
        _indexerOptions = Microsoft.Extensions.Options.Options.Create(new IndexerOptions
        {
            DefaultSourcePath = "/source"
        });
        _geminiOptions = Microsoft.Extensions.Options.Options.Create(new GeminiOptions
        {
            ApiKey = "test-key",
            ChatModel = "gemini-2.5-flash"
        });
    }

    [Fact]
    public async Task ScanDirectoryCommand_PopulatesProjectsList_AndUpdatesStatus()
    {
        // Arrange
        var discovered = new List<Project>
        {
            new Project { Name = "Project1", Path = "/source/Project1", IndexedAt = DateTime.UtcNow },
            new Project { Name = "Project2", Path = "/source/Project2", IndexedAt = null }
        };

        _mockScanner.Setup(s => s.DiscoverProjectsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(discovered);

        var vm = new MainViewModel(_mockScanner.Object, _mockIndexer.Object, _mockRagSearch.Object, _indexerOptions, _geminiOptions);

        // Act
        await vm.ScanDirectoryCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, vm.Projects.Count);
        Assert.True(vm.HasUnindexedProjects);
        Assert.Equal("Project1", vm.SelectedProject?.Name);
        _mockScanner.Verify(s => s.DiscoverProjectsAsync("/source", It.IsAny<CancellationToken>()), Times.Once);
        _mockIndexer.Verify(i => i.IndexProjectAsync(It.IsAny<Project>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ExcludeProjectCommand_RemovesUnindexedProject_FromList()
    {
        // Arrange
        var unindexed = new Project { Name = "ProjectUnindexed", Path = "/source/ProjectUnindexed", IndexedAt = null };
        var indexed = new Project { Name = "ProjectIndexed", Path = "/source/ProjectIndexed", IndexedAt = DateTime.UtcNow };

        var vm = new MainViewModel(_mockScanner.Object, _mockIndexer.Object, _mockRagSearch.Object, _indexerOptions, _geminiOptions);
        vm.Projects.Add(unindexed);
        vm.Projects.Add(indexed);

        // Act - Exclude unindexed project
        vm.ExcludeProjectCommand.Execute(unindexed);

        // Assert
        Assert.Single(vm.Projects);
        Assert.DoesNotContain(unindexed, vm.Projects);
        Assert.Contains(indexed, vm.Projects);
        Assert.False(vm.HasUnindexedProjects);
    }

    [Fact]
    public void ExcludeProjectCommand_DoesNotRemoveAlreadyIndexedProject()
    {
        // Arrange
        var indexed = new Project { Name = "ProjectIndexed", Path = "/source/ProjectIndexed", IndexedAt = DateTime.UtcNow };

        var vm = new MainViewModel(_mockScanner.Object, _mockIndexer.Object, _mockRagSearch.Object, _indexerOptions, _geminiOptions);
        vm.Projects.Add(indexed);

        // Act - Attempt to exclude indexed project
        vm.ExcludeProjectCommand.Execute(indexed);

        // Assert - Should still be present
        Assert.Single(vm.Projects);
        Assert.Contains(indexed, vm.Projects);
    }

    [Fact]
    public async Task IndexNewProjectsCommand_CallsIndexerOnlyForUnindexedProjects()
    {
        // Arrange
        var indexed = new Project { Name = "Project1", Path = "/source/Project1", IndexedAt = DateTime.UtcNow };
        var unindexed1 = new Project { Name = "Project2", Path = "/source/Project2", IndexedAt = null };
        var unindexed2 = new Project { Name = "Project3", Path = "/source/Project3", IndexedAt = null };

        _mockIndexer.Setup(i => i.IndexProjectAsync(It.IsAny<Project>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var vm = new MainViewModel(_mockScanner.Object, _mockIndexer.Object, _mockRagSearch.Object, _indexerOptions, _geminiOptions);
        vm.Projects.Add(indexed);
        vm.Projects.Add(unindexed1);
        vm.Projects.Add(unindexed2);

        // Act
        await vm.IndexNewProjectsCommand.ExecuteAsync(null);

        // Assert
        _mockIndexer.Verify(i => i.IndexProjectAsync(unindexed1, It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockIndexer.Verify(i => i.IndexProjectAsync(unindexed2, It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockIndexer.Verify(i => i.IndexProjectAsync(indexed, It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(vm.HasUnindexedProjects);
    }

    [Fact]
    public async Task AskQuestionCommand_CallsRagSearch_AndSetsAnswer()
    {
        // Arrange
        var indexed = new Project { Name = "Project1", Path = "/source/Project1", IndexedAt = DateTime.UtcNow };
        var chunk = new CodeChunk { FilePath = "/source/Project1/Service.cs", ClassName = "Service" };
        var question = "¿Cómo está implementado el servicio?";
        var selectedModel = "gemini-2.5-flash";

        _mockRagSearch.Setup(r => r.AskQuestionAsync(indexed.Id, question, selectedModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagSearchResult
            {
                Answer = "Respuesta de ejemplo",
                CitedChunks = new List<CodeChunk> { chunk }
            });

        var vm = new MainViewModel(_mockScanner.Object, _mockIndexer.Object, _mockRagSearch.Object, _indexerOptions, _geminiOptions);
        vm.Projects.Add(indexed);
        vm.SelectedProject = indexed;
        vm.SearchQuery = question;

        // Act
        await vm.AskQuestionCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("Respuesta de ejemplo", vm.ResponseAnswer);
        Assert.Single(vm.CitedChunks);
        Assert.Equal(chunk, vm.CitedChunks[0]);
        _mockRagSearch.Verify(r => r.AskQuestionAsync(indexed.Id, question, selectedModel, It.IsAny<CancellationToken>()), Times.Once);
    }
}
