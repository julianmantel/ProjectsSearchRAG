using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;
using ProjectsSearchRAG.Data.Context;
using ProjectsSearchRAG.Data.Options;
using ProjectsSearchRAG.Data.Repositories;
using ProjectsSearchRAG.Data.Repositories.IRepository;

namespace ProjectsSearchRAG.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        var configured = configuration.GetConnectionString("DefaultConnection")
                         ?? configuration.GetSection("ConnectionStrings:DefaultConnection").Value
                         ?? string.Empty;

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var conn = !string.IsNullOrWhiteSpace(dbOptions.DefaultConnection)
                ? dbOptions.DefaultConnection
                : configured;

            options.UseNpgsql(conn, npgsqlOptions =>
            {
                npgsqlOptions.UseVector();
            });
        });

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ICodeChunkRepository, CodeChunkRepository>();

        return services;
    }
}
