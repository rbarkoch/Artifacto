using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;

namespace Artifacto.Database.Models;

[Index(nameof(Key), IsUnique = true)]
/// <summary>
/// Represents a project entity in the database.
/// This is the database model for projects, containing the physical storage structure.
/// </summary>
public class Project
{
    /// <summary>
    /// Gets or sets the unique identifier for the project.
    /// </summary>
    [Key]
    public int ProjectId { get; set; }
    
    /// <summary>
    /// Gets or sets the unique key that identifies the project.
    /// </summary>
    public required string Key { get; set; }
    
    /// <summary>
    /// Gets or sets the optional display name for the project.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Gets or sets the optional description for the project.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the collection of artifacts associated with this project.
    /// </summary>
    public ICollection<Artifact> Artifacts { get; set; } = [];
}

/// <summary>
/// Provides extension methods for converting between database and domain project models.
/// </summary>
public static class ProjectMappingExtensions
{
    /// <summary>
    /// Converts a database project entity to a domain project model.
    /// </summary>
    /// <param name="project">The database project entity to convert.</param>
    /// <returns>A domain project model.</returns>
    public static Artifacto.Models.Project ToDomainModel(this Project project)
    {
        return new Artifacto.Models.Project(
            ProjectId: project.ProjectId,
            Key: project.Key,
            Name: project.Name,
            Description: project.Description
            // Note: ArtifactCount is not included here as it should be calculated in the repository
        );
    }

    /// <summary>
    /// Converts a domain project model to a database project entity.
    /// </summary>
    /// <param name="project">The domain project model to convert.</param>
    /// <returns>A database project entity.</returns>
    public static Project ToDatabaseModel(this Artifacto.Models.Project project)
    {
        return new Project
        {
            ProjectId = project.ProjectId,
            Key = project.Key,
            Name = project.Name,
            Description = project.Description
        };
    }
}
