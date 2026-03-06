using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Artifacto.Models;

using Microsoft.Extensions.Logging;

using OneOf;
using OneOf.Types;

using Version = Artifacto.Models.Version;

namespace Artifacto.FileStorage;

/// <summary>
/// Provides file system-based storage implementation for artifacts and projects.
/// This class manages the physical storage of project directories and artifact files on the file system.
/// </summary>
public class ArtifactoFileStorage : IArtifactoFileStorage
{
    private const string ArtifactContentFileName = "artifact.bin";
    private const string SbomContentFileName = "sbom.json";

    private readonly ILogger<ArtifactoFileStorage> _logger;
    private readonly string _basePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactoFileStorage"/> class.
    /// </summary>
    /// <param name="logger">The logger for recording operations.</param>
    /// <param name="basePath">The base path where projects and artifacts will be stored.</param>
    public ArtifactoFileStorage(ILogger<ArtifactoFileStorage> logger, string basePath)
    {
        _logger = logger;
        _basePath = Path.GetFullPath(basePath);
    }

    /// <summary>
    /// Gets the directory path for a project.
    /// </summary>
    /// <param name="projectKey">The project key.</param>
    /// <returns>The full path to the project directory.</returns>
    private string GetProjectPath(string projectKey)
    {
        string projectPath = Path.Combine(_basePath, projectKey);
        string fullPath = Path.GetFullPath(projectPath);
        
        // Ensure the resolved path is still within the base path to prevent directory traversal attacks
        if (!fullPath.StartsWith(_basePath + Path.DirectorySeparatorChar) && fullPath != _basePath)
        {
            throw new UnauthorizedAccessException($"Access to path '{projectKey}' is denied.");
        }
        
        return fullPath;
    }
        
    /// <summary>
    /// Gets the storage path for an artifact version.
    /// </summary>
    /// <param name="projectKey">The project key.</param>
    /// <param name="artifactVersion">The artifact version.</param>
    /// <returns>The full storage path for the artifact version.</returns>
    private string GetArtifactVersionPath(string projectKey, string artifactVersion)
    {
        string artifactPath = Path.Combine(_basePath, projectKey, artifactVersion);
        string fullPath = Path.GetFullPath(artifactPath);
        
        // Ensure the resolved path is still within the base path to prevent directory traversal attacks
        if (!fullPath.StartsWith(_basePath + Path.DirectorySeparatorChar) && fullPath != _basePath)
        {
            throw new UnauthorizedAccessException($"Access to path '{projectKey}/{artifactVersion}' is denied.");
        }
        
        return fullPath;
    }

    /// <summary>
    /// Gets the canonical file path for the artifact content within a version directory.
    /// </summary>
    /// <param name="projectKey">The project key.</param>
    /// <param name="artifactVersion">The artifact version.</param>
    /// <returns>The full path to the stored artifact content.</returns>
    private string GetArtifactContentPath(string projectKey, string artifactVersion)
    {
        return Path.Combine(GetArtifactVersionPath(projectKey, artifactVersion), ArtifactContentFileName);
    }

    /// <summary>
    /// Gets the canonical file path for the stored SBOM within a version directory.
    /// </summary>
    /// <param name="projectKey">The project key.</param>
    /// <param name="artifactVersion">The artifact version.</param>
    /// <returns>The full path to the stored SBOM.</returns>
    private string GetArtifactSbomPath(string projectKey, string artifactVersion)
    {
        return Path.Combine(GetArtifactVersionPath(projectKey, artifactVersion), SbomContentFileName);
    }

    /// <summary>
    /// Ensures that the artifact version uses the directory-based layout.
    /// </summary>
    /// <param name="projectKey">The project key.</param>
    /// <param name="artifactVersion">The artifact version.</param>
    /// <returns>The artifact version directory path.</returns>
    private string EnsureArtifactDirectoryLayout(string projectKey, string artifactVersion)
    {
        string projectPath = GetProjectPath(projectKey);
        if (!Directory.Exists(projectPath))
        {
            Directory.CreateDirectory(projectPath);
        }

        string artifactVersionPath = GetArtifactVersionPath(projectKey, artifactVersion);
        if (Directory.Exists(artifactVersionPath))
        {
            return artifactVersionPath;
        }

        if (File.Exists(artifactVersionPath))
        {
            string migrationFilePath = Path.Combine(projectPath, $"{artifactVersion}.{Guid.NewGuid():N}.migration");
            File.Move(artifactVersionPath, migrationFilePath);
            Directory.CreateDirectory(artifactVersionPath);
            File.Move(migrationFilePath, GetArtifactContentPath(projectKey, artifactVersion));
            return artifactVersionPath;
        }

        Directory.CreateDirectory(artifactVersionPath);
        return artifactVersionPath;
    }

    /// <inheritdoc />
    public async Task<OneOf<Success, BadRequestError, ConflictError>> NewProject(string projectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new project directory {ProjectKey}", projectKey);
        
        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        string projectPath = GetProjectPath(projectKey);
        if (Directory.Exists(projectPath))
        {
            _logger.LogWarning("Project directory already exists {ProjectKey} at {ProjectPath}", projectKey, projectPath);
            return new ConflictError($"Project with key '{projectKey}' already exists.");
        }

        Directory.CreateDirectory(projectPath);
        _logger.LogInformation("Created project directory {ProjectKey} at {ProjectPath}", projectKey, projectPath);

        return new Success();
    }

    /// <inheritdoc />
    public async Task<OneOf<Success, BadRequestError, NotFoundError>> DeleteProjectAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting project directory {ProjectKey}", projectKey);
        
        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        string projectPath = GetProjectPath(projectKey);
        if (!Directory.Exists(projectPath))
        {
            _logger.LogInformation("Project directory not found {ProjectKey} at {ProjectPath}", projectKey, projectPath);
            return new NotFoundError($"Project with key '{projectKey}' not found.");
        }

        Directory.Delete(projectPath, true);
        _logger.LogInformation("Deleted project directory {ProjectKey} at {ProjectPath}", projectKey, projectPath);
        return new Success();
    }

    /// <inheritdoc />
    public async Task<OneOf<Success, BadRequestError, NotFoundError>> RenameProject(string oldProjectKey, string newProjectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Renaming project directory from {OldProjectKey} to {NewProjectKey}", oldProjectKey, newProjectKey);
        
        if (!Project.ValidateKey(oldProjectKey) || !Project.ValidateKey(newProjectKey))
        {
            _logger.LogWarning("Invalid project key format oldKey={OldProjectKey} newKey={NewProjectKey}", oldProjectKey, newProjectKey);
            return new BadRequestError("Invalid project key format.");
        }

        string oldProjectPath = GetProjectPath(oldProjectKey);
        if (!Directory.Exists(oldProjectPath))
        {
            _logger.LogInformation("Source project directory not found {OldProjectKey} at {OldProjectPath}", oldProjectKey, oldProjectPath);
            return new NotFoundError($"Project with key '{oldProjectKey}' not found.");
        }

        string newProjectPath = GetProjectPath(newProjectKey);
        Directory.Move(oldProjectPath, newProjectPath);
        _logger.LogInformation("Renamed project directory from {OldProjectPath} to {NewProjectPath}", oldProjectPath, newProjectPath);
        return new Success();
    }


    /// <inheritdoc />
    public async Task<OneOf<SaveArtifactSuccess, BadRequestError>> SaveArtifactAsync(string projectKey, string artifactVersion, Stream artifactStream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving artifact {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);
        
        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        if (!Version.TryParse(artifactVersion, out Version parsedVersion))
        {
            _logger.LogWarning("Invalid artifact version format {ArtifactVersion} for project {ProjectKey}", artifactVersion, projectKey);
            return new BadRequestError("Invalid artifact version format.");
        }

        string normalizedVersion = parsedVersion.ToString();
        string versionDirectoryPath = EnsureArtifactDirectoryLayout(projectKey, normalizedVersion);
        string path = GetArtifactContentPath(projectKey, normalizedVersion);

        using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        string sha256Hash = await HashCalculator.CalculateSha256HashAsync(artifactStream, fileStream, cancellationToken);
        
        _logger.LogInformation("Saved artifact {ProjectKey} version {ArtifactVersion} to {Path} with hash {Hash}", projectKey, normalizedVersion, path, sha256Hash);
        return new SaveArtifactSuccess(sha256Hash);
    }

    /// <inheritdoc />
    public async Task<OneOf<SaveArtifactSuccess, BadRequestError>> SaveArtifactSbomAsync(string projectKey, string artifactVersion, Stream sbomStream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Saving SBOM {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);

        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        if (!Version.TryParse(artifactVersion, out Version parsedVersion))
        {
            _logger.LogWarning("Invalid artifact version format {ArtifactVersion} for project {ProjectKey}", artifactVersion, projectKey);
            return new BadRequestError("Invalid artifact version format.");
        }

        string normalizedVersion = parsedVersion.ToString();
        string versionDirectoryPath = EnsureArtifactDirectoryLayout(projectKey, normalizedVersion);
        string path = GetArtifactSbomPath(projectKey, normalizedVersion);

        using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        string sha256Hash = await HashCalculator.CalculateSha256HashAsync(sbomStream, fileStream, cancellationToken);

        _logger.LogInformation("Saved SBOM {ProjectKey} version {ArtifactVersion} to {Path} with hash {Hash}", projectKey, normalizedVersion, path, sha256Hash);
        return new SaveArtifactSuccess(sha256Hash);
    }

    /// <inheritdoc />
    public async Task<OneOf<Stream, BadRequestError, NotFoundError>> DownloadArtifactAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading artifact {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);
        
        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        if (!Version.TryParse(artifactVersion, out _))
        {
            _logger.LogWarning("Invalid artifact version format {ArtifactVersion} for project {ProjectKey}", artifactVersion, projectKey);
            return new BadRequestError("Invalid artifact version format.");
        }

        string projectPath = Path.Combine(_basePath, projectKey);
        if (!Directory.Exists(projectPath))
        {
            _logger.LogInformation("Project directory not found {ProjectKey} at {ProjectPath}", projectKey, projectPath);
            return new NotFoundError();
        }

        string artifactVersionPath = GetArtifactVersionPath(projectKey, artifactVersion);
        string path;
        if (Directory.Exists(artifactVersionPath))
        {
            path = GetArtifactContentPath(projectKey, artifactVersion);
        }
        else
        {
            path = artifactVersionPath;
        }

        if (!File.Exists(path))
        {
            _logger.LogInformation("Artifact file not found {ProjectKey} version {ArtifactVersion} at {Path}", projectKey, artifactVersion, path);
            return new NotFoundError();
        }

        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        _logger.LogDebug("Opened artifact file stream {ProjectKey} version {ArtifactVersion} from {Path}", projectKey, artifactVersion, path);
        return stream;
    }

    /// <inheritdoc />
    public async Task<OneOf<Stream, BadRequestError, NotFoundError>> DownloadArtifactSbomAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading SBOM {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);

        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        if (!Version.TryParse(artifactVersion, out _))
        {
            _logger.LogWarning("Invalid artifact version format {ArtifactVersion} for project {ProjectKey}", artifactVersion, projectKey);
            return new BadRequestError("Invalid artifact version format.");
        }

        string artifactVersionPath = GetArtifactVersionPath(projectKey, artifactVersion);
        if (!Directory.Exists(artifactVersionPath))
        {
            _logger.LogInformation("Artifact version directory not found {ProjectKey} version {ArtifactVersion} at {Path}", projectKey, artifactVersion, artifactVersionPath);
            return new NotFoundError();
        }

        string path = GetArtifactSbomPath(projectKey, artifactVersion);
        if (!File.Exists(path))
        {
            _logger.LogInformation("SBOM file not found {ProjectKey} version {ArtifactVersion} at {Path}", projectKey, artifactVersion, path);
            return new NotFoundError();
        }

        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        _logger.LogDebug("Opened SBOM file stream {ProjectKey} version {ArtifactVersion} from {Path}", projectKey, artifactVersion, path);
        return stream;
    }

    /// <inheritdoc />
    public async Task<OneOf<Success, BadRequestError, NotFoundError>> DeleteArtifactSbomAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting SBOM {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);

        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        if (!Version.TryParse(artifactVersion, out _))
        {
            _logger.LogWarning("Invalid artifact version format {ArtifactVersion} for project {ProjectKey}", artifactVersion, projectKey);
            return new BadRequestError("Invalid artifact version format.");
        }

        string artifactVersionPath = GetArtifactVersionPath(projectKey, artifactVersion);
        if (!Directory.Exists(artifactVersionPath))
        {
            _logger.LogInformation("Artifact version directory not found {ProjectKey} version {ArtifactVersion} at {Path}", projectKey, artifactVersion, artifactVersionPath);
            return new NotFoundError();
        }

        string path = GetArtifactSbomPath(projectKey, artifactVersion);
        if (!File.Exists(path))
        {
            _logger.LogInformation("SBOM file not found {ProjectKey} version {ArtifactVersion} at {Path}", projectKey, artifactVersion, path);
            return new NotFoundError();
        }

        File.Delete(path);
        _logger.LogInformation("Deleted SBOM {ProjectKey} version {ArtifactVersion} from {Path}", projectKey, artifactVersion, path);
        return new Success();
    }

    /// <inheritdoc />
    public async Task<OneOf<Success, BadRequestError, NotFoundError>> DeleteArtifactAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting artifact {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);
        
        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        if (!Version.TryParse(artifactVersion, out _))
        {
            _logger.LogWarning("Invalid artifact version format {ArtifactVersion} for project {ProjectKey}", artifactVersion, projectKey);
            return new BadRequestError("Invalid artifact version format.");
        }

        string projectPath = Path.Combine(_basePath, projectKey);
        if (!Directory.Exists(projectPath))
        {
            _logger.LogInformation("Project directory not found {ProjectKey} at {ProjectPath}", projectKey, projectPath);
            return new NotFoundError($"Project with key {projectKey} not found.");
        }

        string path = GetArtifactVersionPath(projectKey, artifactVersion);
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            _logger.LogInformation("Artifact file not found {ProjectKey} version {ArtifactVersion} at {Path}", projectKey, artifactVersion, path);
            return new NotFoundError($"Artifact with version {artifactVersion} not found.");
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        else
        {
            File.Delete(path);
        }

        _logger.LogInformation("Deleted artifact {ProjectKey} version {ArtifactVersion} from {Path}", projectKey, artifactVersion, path);
        return new Success();
    }

    /// <inheritdoc />
    public async Task<OneOf<Success, BadRequestError, NotFoundError, ConflictError>> ReversionArtifactAsync(string projectKey, string sourceArtifactVersion, string targetArtifactVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reversioning artifact {ProjectKey} from version {SourceVersion} to {TargetVersion}", projectKey, sourceArtifactVersion, targetArtifactVersion);
        
        if (!Project.ValidateKey(projectKey))
        {
            _logger.LogWarning("Invalid project key format {ProjectKey}", projectKey);
            return new BadRequestError("Invalid project key format.");
        }

        if (!Version.TryParse(sourceArtifactVersion, out _))
        {
            _logger.LogWarning("Invalid source artifact version format {SourceVersion} for project {ProjectKey}", sourceArtifactVersion, projectKey);
            return new BadRequestError("Invalid source artifact version format.");
        }

        if (!Version.TryParse(targetArtifactVersion, out _))
        {
            _logger.LogWarning("Invalid target artifact version format {TargetVersion} for project {ProjectKey}", targetArtifactVersion, projectKey);
            return new BadRequestError("Invalid target artifact version format.");
        }

        if (sourceArtifactVersion == targetArtifactVersion)
        {
            _logger.LogWarning("Source and target versions are the same {Version} for project {ProjectKey}", sourceArtifactVersion, projectKey);
            return new BadRequestError("Source and target artifact versions cannot be the same.");
        }

        string sourcePath = GetArtifactVersionPath(projectKey, sourceArtifactVersion);
        bool sourceExists = Directory.Exists(sourcePath) || File.Exists(sourcePath);
        if (!sourceExists)
        {
            _logger.LogInformation("Source artifact file not found {ProjectKey} version {SourceVersion} at {SourcePath}", projectKey, sourceArtifactVersion, sourcePath);
            return new NotFoundError($"Source artifact with version '{sourceArtifactVersion}' not found.");
        }

        string targetPath = GetArtifactVersionPath(projectKey, targetArtifactVersion);
        if (Directory.Exists(targetPath) || File.Exists(targetPath))
        {
            _logger.LogWarning("Target artifact file already exists {ProjectKey} version {TargetVersion} at {TargetPath}", projectKey, targetArtifactVersion, targetPath);
            return new ConflictError($"Target artifact with version '{targetArtifactVersion}' already exists.");
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, targetPath);
        }
        else
        {
            File.Move(sourcePath, targetPath);
        }

        _logger.LogInformation("Reversioned artifact {ProjectKey} from {SourcePath} to {TargetPath}", projectKey, sourcePath, targetPath);

        return new Success();
    }
}
