using Google.GenAI;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectsSearchRAG.Core.Clients;
using ProjectsSearchRAG.Core.Clients.IClients;
using ProjectsSearchRAG.Core.Options;
using ProjectsSearchRAG.Core.Services;
using ProjectsSearchRAG.Core.Services.IService;
using ProjectsSearchRAG.Data;

namespace ProjectsSearchRAG.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddData(configuration);

        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.Configure<IndexerOptions>(configuration.GetSection(IndexerOptions.SectionName));

        services.AddScoped<IProjectScannerService, ProjectScannerService>();
        services.AddScoped<ICodeChunkerService, CodeChunkerService>();
        services.AddScoped<ICodeIndexerService, CodeIndexerService>();
        services.AddScoped<IRagSearchService, RagSearchService>();
        services.AddScoped<IGeminiEmbeddingClient, GeminiEmbeddingClient>();
        services.AddScoped<IGeminiChatClient, GeminiChatClient>();

        return services;
    }
}
