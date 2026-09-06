using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace ProjectsSearchRAG.Data.Context;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseNpgsql(GetConnectionString(), npgsqlOptions =>
        {
            npgsqlOptions.UseVector();
        });

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection environment variable is not set. " +
                "Define it before running 'dotnet ef' (see README.md / .env.example).");
        }

        return connectionString;
    }
}