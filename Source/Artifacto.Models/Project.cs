using System.Text.RegularExpressions;

namespace Artifacto.Models;

/// <summary>
/// Represents a project that contains versioned artifacts.
/// Projects are uniquely identified by their key and can contain multiple artifacts.
/// </summary>
/// <param name="ProjectId">The unique identifier for the project.</param>
/// <param name="Key">The unique key that identifies the project. Must follow the pattern defined in <see cref="ProjectKeyPattern"/>.</param>
/// <param name="Name">The optional display name for the project.</param>
/// <param name="Description">The optional description for the project.</param>
/// <param name="ArtifactCount">The number of artifacts currently stored in this project.</param>
/// <param name="LatestStableVersion">The latest stable (non-prerelease) version of artifacts in this project.</param>
/// <param name="LatestVersion">The latest version (including prerelease) of artifacts in this project.</param>
/// <param name="LatestStableVersionUploadDate">The upload date of the latest stable version, if available.</param>
/// <param name="LatestVersionUploadDate">The upload date of the latest version, if available.</param>
public record Project(
        int ProjectId,
        string Key,
        string? Name = null,
        string? Description = null,
        int ArtifactCount = 0,
        string? LatestStableVersion = null,
        string? LatestVersion = null,
        DateTime? LatestStableVersionUploadDate = null,
        DateTime? LatestVersionUploadDate = null
    )
{
    /// <summary>
    /// Regular expression pattern that defines valid project keys.
    /// Keys must contain only lowercase letters, digits, and dashes, and cannot start or end with a dash.
    /// </summary>
    public const string ProjectKeyPattern = @"^[a-z0-9]+(-?[a-z0-9]+)*$";

    /// <summary>
    /// Gets the unique identifier for the project.
    /// </summary>
    public int ProjectId { get; init; } = ProjectId;
    
    /// <summary>
    /// Gets the unique key that identifies the project.
    /// </summary>
    public string Key { get; init; } = AssignKey(Key);
    
    /// <summary>
    /// Gets the optional display name for the project.
    /// </summary>
    public string? Name { get; init; } = Name;
    
    /// <summary>
    /// Gets the optional description for the project.
    /// </summary>
    public string? Description { get; init; } = Description;
    
    /// <summary>
    /// Gets the number of artifacts currently stored in this project.
    /// </summary>
    public int ArtifactCount { get; init; } = ArtifactCount;
    
    /// <summary>
    /// Gets the latest stable (non-prerelease) version of artifacts in this project.
    /// </summary>
    public string? LatestStableVersion { get; init; } = LatestStableVersion;
    
    /// <summary>
    /// Gets the latest version (including prerelease) of artifacts in this project.
    /// </summary>
    public string? LatestVersion { get; init; } = LatestVersion;
    
    /// <summary>
    /// Gets the upload date of the latest stable version, if available.
    /// </summary>
    public DateTime? LatestStableVersionUploadDate { get; init; } = LatestStableVersionUploadDate;
    
    /// <summary>
    /// Gets the upload date of the latest version, if available.
    /// </summary>
    public DateTime? LatestVersionUploadDate { get; init; } = LatestVersionUploadDate;

    /// <summary>
    /// Assigns and validates a project key.
    /// </summary>
    /// <param name="key">The key to validate and assign.</param>
    /// <returns>The validated key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the key is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when the key doesn't match the required pattern.</exception>
    private static string AssignKey(string key)
    {
        ThrowIfInvalidKey(key);
        return key;
    }

    /// <summary>
    /// Validates a project key and throws an exception if it's invalid.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when the key is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when the key doesn't match the required pattern.</exception>
    public static void ThrowIfInvalidKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentNullException(nameof(key), "Key is required.");
        }

        if (!Regex.IsMatch(key, ProjectKeyPattern))
        {
            throw new ArgumentException("Key can only contain lowercase letters, digits, and dashes.", nameof(key));
        }
    }

    /// <summary>
    /// Validates whether a project key is valid without throwing an exception.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <returns><c>true</c> if the key is valid; otherwise, <c>false</c>.</returns>
    public static bool ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!Regex.IsMatch(key, ProjectKeyPattern))
        {
            return false;
        }

        return true;
    }
}
