namespace Artifacto.Models;

/// <summary>
/// Represents a versioned artifact within a project.
/// Artifacts are files that are stored and managed within the Artifacto system.
/// </summary>
/// <param name="ProjectId">The unique identifier of the project that contains this artifact.</param>
/// <param name="ArtifactId">The unique identifier for this artifact.</param>
/// <param name="Version">The version of this artifact.</param>
/// <param name="FileName">The original filename of the artifact.</param>
/// <param name="FileSizeBytes">The size of the artifact file in bytes.</param>
/// <param name="Sha256Hash">The SHA256 hash of the artifact file.</param>
/// <param name="Timestamp">The timestamp when the artifact was created or last modified.</param>
/// <param name="Retained">Whether this artifact should be retained and not automatically deleted.</param>
/// <param name="Locked">Whether this artifact is locked and cannot be modified or deleted.</param>
public record Artifact(
    int ProjectId,
    int ArtifactId,
    Version Version,
    string FileName,
    ulong FileSizeBytes,
    string Sha256Hash,
    DateTime Timestamp,
    bool Retained,
    bool Locked
);
