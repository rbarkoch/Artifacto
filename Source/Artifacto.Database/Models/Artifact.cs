#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

using Version = Artifacto.Models.Version;

namespace Artifacto.Database.Models;

[Index(nameof(ProjectId), nameof(Version), IsUnique = true)]
/// <summary>
/// Represents an artifact entity in the database.
/// This is the database model for artifacts, containing the physical storage structure.
/// </summary>
public class Artifact
{
    /// <summary>
    /// Gets or sets the unique identifier for the artifact.
    /// </summary>
    [Key]
    public int ArtifactId { get; set; }
    
    /// <summary>
    /// Gets or sets the version string of the artifact.
    /// </summary>
    public string Version { get; set; }
    
    /// <summary>
    /// Gets or sets the optional description of the artifact.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the original filename of the artifact.
    /// </summary>
    public string FileName { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the artifact file in bytes.
    /// </summary>
    public ulong FileSizeBytes { get; set; }
    
    /// <summary>
    /// Gets or sets the SHA256 hash of the artifact file.
    /// </summary>
    public string Sha256Hash { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when the artifact was created.
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether this artifact should be retained.
    /// </summary>
    public bool Retained { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether this artifact is locked from modification.
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Gets or sets the foreign key reference to the project that contains this artifact.
    /// </summary>
    [ForeignKey("Project")]
    public int ProjectId { get; set; }
    
    /// <summary>
    /// Gets or sets the project that contains this artifact.
    /// </summary>
    public Project Project { get; set; }
}

/// <summary>
/// Provides extension methods for converting between database and domain artifact models.
/// </summary>
public static class ArtifactMappingExtensions
{
    /// <summary>
    /// Converts a database artifact entity to a domain artifact model.
    /// </summary>
    /// <param name="artifact">The database artifact entity to convert.</param>
    /// <returns>A domain artifact model.</returns>
    public static Artifacto.Models.Artifact ToDomainModel(this Artifact artifact)
    {
        return new Artifacto.Models.Artifact(
            ProjectId: artifact.ProjectId,
            ArtifactId: artifact.ArtifactId,
            Version: Version.Parse(artifact.Version),
            FileName: artifact.FileName,
            FileSizeBytes: artifact.FileSizeBytes,
            Sha256Hash: artifact.Sha256Hash,
            Timestamp: artifact.Timestamp,
            Retained: artifact.Retained,
            Locked: artifact.Locked
        );
    }

    /// <summary>
    /// Converts a domain artifact model to a database artifact entity.
    /// </summary>
    /// <param name="artifact">The domain artifact model to convert.</param>
    /// <returns>A database artifact entity.</returns>
    public static Artifact ToDatabaseModel(this Artifacto.Models.Artifact artifact)
    {
        return new Artifact
        {
            Version = artifact.Version.ToString(),
            FileName = artifact.FileName,
            FileSizeBytes = artifact.FileSizeBytes,
            Sha256Hash = artifact.Sha256Hash,
            Timestamp = artifact.Timestamp,
            Retained = artifact.Retained,
            Locked = artifact.Locked
        };
    }

}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
