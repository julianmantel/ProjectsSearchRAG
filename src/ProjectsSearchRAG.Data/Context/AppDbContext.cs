using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using ProjectsSearchRAG.Data.Models;

namespace ProjectsSearchRAG.Data.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CodeChunk> CodeChunks => Set<CodeChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Path).HasColumnName("path").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.IndexedAt).HasColumnName("indexed_at");

            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Path).IsUnique();
        });

        modelBuilder.Entity<CodeChunk>(entity =>
        {
            entity.ToTable("code_chunks");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ProjectId).HasColumnName("project_id").IsRequired();
            entity.Property(e => e.FilePath).HasColumnName("file_path").IsRequired();
            entity.Property(e => e.Language).HasColumnName("language");
            entity.Property(e => e.ClassName).HasColumnName("class_name");
            entity.Property(e => e.MethodName).HasColumnName("method_name");
            entity.Property(e => e.StartLine).HasColumnName("start_line");
            entity.Property(e => e.EndLine).HasColumnName("end_line");
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.CodeChunks)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ProjectId)
                  .HasDatabaseName("idx_code_chunks_project_id");

            entity.HasIndex(e => new { e.ProjectId, e.FilePath })
                  .HasDatabaseName("idx_code_chunks_file_path");

            entity.HasIndex(e => e.Embedding)
                  .HasMethod("hnsw")
                  .HasOperators("vector_cosine_ops")
                  .HasDatabaseName("idx_code_chunks_embedding");
        });
    }
}
