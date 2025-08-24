using System.Threading;
using System.Threading.Tasks;

using Artifacto.Database.Models;

using Microsoft.EntityFrameworkCore;

namespace Artifacto.Database;

/// <summary>
/// The main database context for the Artifacto application.
/// Provides access to projects and artifacts data stores.
/// </summary>
public class ArtifactoDbContext : DbContext
{
    /// <summary>
    /// Gets or sets the collection of projects in the database.
    /// </summary>
    public DbSet<Project> Projects { get; set; }
    
    /// <summary>
    /// Gets or sets the collection of artifacts in the database.
    /// </summary>
    public DbSet<Artifact> Artifacts { get; set; }

    /// <summary>
    /// Configures the entity model relationships and constraints.
    /// </summary>
    /// <param name="modelBuilder">The model builder used to configure the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>()
            .HasKey(p => p.ProjectId);
        modelBuilder.Entity<Artifact>()
            .HasKey(a => a.ArtifactId);
        modelBuilder.Entity<Project>()
            .HasMany(p => p.Artifacts)
            .WithOne(a => a.Project)
            .HasForeignKey(a => a.ProjectId);
    }
}

/// <summary>
/// Provides extension methods for the ArtifactoDbContext to simplify common query operations.
/// </summary>
public static class ArtifactoDbContextExtensions
{
    /// <summary>
    /// Finds a project by its unique key.
    /// </summary>
    /// <param name="projects">The projects DbSet to search in.</param>
    /// <param name="key">The unique key of the project to find.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The project if found, otherwise null.</returns>
    public static Task<Project?> FindByKeyAsync(this DbSet<Project> projects, string key, CancellationToken cancellationToken = default)
    {
        return projects.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);
    }

    /// <summary>
    /// Finds an artifact by project ID and version.
    /// </summary>
    /// <param name="artifacts">The artifacts DbSet to search in.</param>
    /// <param name="projectId">The ID of the project containing the artifact.</param>
    /// <param name="artifactVersion">The version of the artifact to find.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The artifact if found, otherwise null.</returns>
    public static Task<Artifact?> FindByVersionAsync(this DbSet<Artifact> artifacts, int projectId, string artifactVersion, CancellationToken cancellationToken = default)
    {
        return artifacts.FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Version == artifactVersion, cancellationToken);
    }
}
