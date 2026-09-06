using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Core.Services.IService;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProjectScannerService _projectScannerService;
    private readonly ICodeIndexerService _codeIndexerService;
    private readonly IRagSearchService _ragSearchService;
    private readonly IndexerOptions _indexerOptions;
    private readonly GeminiOptions _geminiOptions;

    [ObservableProperty]
    private string[] _geminiModels =
    {
        "gemini-2.5-flash",
        "gemini-3.5-flash-lite",
        "gemini-2.5-pro"
    };

    [ObservableProperty]
    private string _title = "Semantic Code Search - RAG";

    [ObservableProperty]
    private string _sourceDirectory;

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _responseAnswer = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasUnindexedProjects;

    [ObservableProperty]
    private string _selectedChatModel = "gemini-2.5-flash";

    [ObservableProperty]
    private string _statusMessage = "Completed. Input base directory and press 'Scan Directory'.";

    public ObservableCollection<Project> Projects { get; } = new();

    public ObservableCollection<CodeChunk> CitedChunks { get; } = new();

    public MainViewModel(
        IProjectScannerService projectScannerService,
        ICodeIndexerService codeIndexerService,
        IRagSearchService ragSearchService,
        IOptions<IndexerOptions> indexerOptions,
        IOptions<GeminiOptions> geminiOptions)
    {
        _projectScannerService = projectScannerService;
        _codeIndexerService = codeIndexerService;
        _ragSearchService = ragSearchService;
        _indexerOptions = indexerOptions.Value;
        _geminiOptions = geminiOptions.Value;
        _sourceDirectory = _indexerOptions.DefaultSourcePath;
        _selectedChatModel = "gemini-2.5-flash";
    }

    partial void OnSelectedChatModelChanged(string value)
        => _geminiOptions.ChatModel = value;

    [RelayCommand]
    public void SelectDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select source directory",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(SourceDirectory) && Directory.Exists(SourceDirectory))
        {
            dialog.InitialDirectory = SourceDirectory;
        }

        if (!dialog.ShowDialog().Value)
        {
            return;
        }
        SourceDirectory = dialog.FolderName;
        StatusMessage = $"Directory selected: {SourceDirectory}. Press 'Scan Directory' to list projects.";
    }

    [RelayCommand]
    public async Task ScanDirectoryAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusMessage = $"Scanning directory: {SourceDirectory}...";

            var discovered = await _projectScannerService.DiscoverProjectsAsync(SourceDirectory);

            Projects.Clear();
            foreach (var p in discovered)
            {
                Projects.Add(p);
            }

            UpdateProjectsCountStatus();

            if (SelectedProject == null && Projects.Count > 0)
            {
                SelectedProject = Projects[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while scanning directory: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void ExcludeProject(Project? project)
    {
        if (project == null) return;

        if (!project.IsIndexed && Projects.Contains(project))
        {
            Projects.Remove(project);
            UpdateProjectsCountStatus();

            if (SelectedProject == project)
            {
                SelectedProject = Projects.FirstOrDefault();
            }
        }
    }

    [RelayCommand]
    public async Task IndexNewProjectsAsync()
    {
        if (IsBusy) return;

        var unindexedProjects = Projects.Where(p => !p.IsIndexed).ToList();
        if (unindexedProjects.Count == 0)
        {
            StatusMessage = "Dont have any unindexed projects to process";
            return;
        }

        if (string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
        {
            StatusMessage = "Configure your Gemini API key in the .env file (Gemini__ApiKey) before indexing.";
            return;
        }

        try
        {
            IsBusy = true;
            int totalProcessed = 0;

            var progress = new Progress<string>(msg =>
            {
                StatusMessage = msg;
            });

            foreach (var project in unindexedProjects)
            {
                StatusMessage = $"Indexing project '{project.Name}'...";
                try
                {
                    await _codeIndexerService.IndexProjectAsync(project, progress);
                    project.IndexedAt = DateTime.UtcNow;
                    totalProcessed++;
                }
                catch (NotImplementedException)
                {
                    StatusMessage = $"Processing '{project.Name}' with CodeIndexerService (in development).";
                }
            }

            var currentList = Projects.ToList();
            Projects.Clear();
            foreach (var p in currentList)
            {
                Projects.Add(p);
            }

            UpdateProjectsCountStatus();
            StatusMessage = $"Indexing completed: {totalProcessed} project(s) processed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during indexing: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AskQuestionAsync()
    {
        if (IsBusy) return;

        var project = SelectedProject;
        if (project is null)
        {
            StatusMessage = "Select a project before asking a question.";
            return;
        }

        if (!project.IsIndexed)
        {
            StatusMessage = $"The project '{project.Name}' is not yet indexed. Please index it first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            StatusMessage = "Enter a question before submitting.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
        {
            StatusMessage = "Configure your Gemini API key in the .env file (Gemini__ApiKey) to use the assistant.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Consulting '{project.Name}': {SearchQuery}";

            var result = await _ragSearchService.AskQuestionAsync(project.Id, SearchQuery, _selectedChatModel);

            ResponseAnswer = result.Answer;

            CitedChunks.Clear();
            foreach (var chunk in result.CitedChunks)
            {
                CitedChunks.Add(chunk);
            }

            StatusMessage = $"Answer generated for '{project.Name}'.";
        }
        catch (NotImplementedException)
        {
            StatusMessage = "RAG search in development (RagSearchService not implemented).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error consulting: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateProjectsCountStatus()
    {
        var indexedCount = Projects.Count(p => p.IsIndexed);
        var unindexedCount = Projects.Count - indexedCount;
        HasUnindexedProjects = unindexedCount > 0;
        StatusMessage = $"Projects in list: {Projects.Count} ({indexedCount} indexed, {unindexedCount} unindexed).";
    }
}
