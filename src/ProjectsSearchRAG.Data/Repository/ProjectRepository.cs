using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProjectsSearchRAG.Data.Context;
using ProjectsSearchRAG.Data.Models;
using ProjectsSearchRAG.Data.Repositories.IRepository;

namespace ProjectsSearchRAG.Data.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Project?> GetByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .FirstOrDefaultAsync(p => p.Path == path, cancellationToken);
    }

    public async Task<Project> UpsertAsync(Project project, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Projects
            .FirstOrDefaultAsync(p => p.Path == project.Path || p.Name == project.Name, cancellationToken);

        if (existing is null)
        {
            await _context.Projects.AddAsync(project, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return project;
        }

        existing.Name = project.Name;
        existing.Path = project.Path;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task UpdateIndexedAtAsync(Guid projectId, DateTime indexedAt, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync([projectId], cancellationToken);
        if (project is not null)
        {
            project.IndexedAt = indexedAt;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync([id], cancellationToken);
        if (project is not null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
