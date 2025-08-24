using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Artifacto.Models;
using Artifacto.Repository;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using OneOf;
using OneOf.Types;

using Version = Artifacto.Models.Version;

namespace Artifacto.WebApi.ControllerImplementations;

/// <summary>
/// Implementation of <see cref="IArtifactsController"/> for managing artifacts.
/// </summary>
public class ArtifactsControllerImplementation : IArtifactsController
{
    private readonly ArtifactsRepository _artifactsRepository;
    private readonly ProjectsRepository _projectsRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactsControllerImplementation"/> class.
    /// </summary>
    /// <param name="projectsRepository">The projects repository.</param>
    /// <param name="artifactsRepository">The artifacts repository.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    private readonly ILogger<ArtifactsControllerImplementation> _logger;

    public ArtifactsControllerImplementation(
        ILogger<ArtifactsControllerImplementation> logger,
        ProjectsRepository projectsRepository,
        ArtifactsRepository artifactsRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _artifactsRepository = artifactsRepository;
        _projectsRepository = projectsRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IActionResult> DeleteArtifactAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default)
    {
    _logger.LogInformation("Request to delete artifact for project {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);
        if (!Version.TryParse(artifactVersion, out Version version))
        {
            _logger.LogWarning("Invalid artifact version format for project {ProjectKey} value {ArtifactVersion}", projectKey, artifactVersion);
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Invalid artifact version format." });
        }

        OneOf<Success, NotFoundError> response = await _artifactsRepository.DeleteArtifactAsync(projectKey, version, cancellationToken);
        if (!response.TryPickT0(out Success _, out NotFoundError notFoundError))
        {
            _logger.LogInformation("Artifact not found for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), notFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = notFoundError.Message });
        }

    _logger.LogInformation("Deleted artifact for project {ProjectKey} version {Version}", projectKey, version.ToString());

        return new NoContentResult();
    }

    public async Task<IActionResult> DownloadArtifactAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default)
    {
    _logger.LogInformation("Request to download artifact for project {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);
        if (!Version.TryParse(artifactVersion, out Version version))
        {
            _logger.LogWarning("Invalid artifact version format for project {ProjectKey} value {ArtifactVersion}", projectKey, artifactVersion);
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Invalid artifact version format." });
        }

        // Get artifact metadata to retrieve the original filename
        OneOf<Artifact, NotFoundError> artifactResponse = await _artifactsRepository.GetArtifactAsync(projectKey, version, cancellationToken);
        if (!artifactResponse.TryPickT0(out Artifact artifact, out NotFoundError artifactNotFoundError))
        {
            _logger.LogInformation("Artifact metadata not found for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), artifactNotFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = artifactNotFoundError.Message });
        }

        OneOf<Stream, NotFoundError> streamResponse = await _artifactsRepository.DownloadArtifactAsync(projectKey, version, cancellationToken);
        if (!streamResponse.TryPickT0(out Stream artifactStream, out NotFoundError streamNotFoundError))
        {
            _logger.LogInformation("Artifact stream not found for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), streamNotFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = streamNotFoundError.Message });
        }

    // Create Content-Disposition header with proper filename encoding
        ContentDispositionHeaderValue contentDisposition = new("attachment");
        contentDisposition.SetMimeFileName(artifact.FileName);

    // Set the Content-Disposition header manually to avoid RFC 5987 encoding
    _httpContextAccessor.HttpContext!.Response.Headers[HeaderNames.ContentDisposition] = contentDisposition.ToString();

    _logger.LogDebug("Returning file stream for project {ProjectKey} version {Version} filename {FileName} size {FileSize}", projectKey, version.ToString(), artifact.FileName, artifact.FileSizeBytes);

        // Return the file stream without FileDownloadName to avoid duplicate header setting
        return new FileStreamResult(artifactStream, "application/octet-stream")
        {
            EnableRangeProcessing = true // Enable range requests for better download experience
        };
    }

    public async Task<ActionResult<ArtifactGetResponse>> GetArtifactAsync(string projectKey, string artifactVersion, CancellationToken cancellationToken = default)
    {
    _logger.LogInformation("Request to get artifact for project {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);
        if (!Version.TryParse(artifactVersion, out Version version))
        {
            _logger.LogWarning("Invalid artifact version format for project {ProjectKey} value {ArtifactVersion}", projectKey, artifactVersion);
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Invalid artifact version format." });
        }

        OneOf<Artifact, NotFoundError> response = await _artifactsRepository.GetArtifactAsync(projectKey, version, cancellationToken);
        if (!response.TryPickT0(out Artifact artifact, out NotFoundError notFoundError))
        {
            _logger.LogInformation("Artifact not found for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), notFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = notFoundError.Message });
        }

    _logger.LogDebug("Found artifact for project {ProjectKey} version {Version} filename {FileName} size {FileSize}", projectKey, artifact.Version.ToString(), artifact.FileName, artifact.FileSizeBytes);

        return new ArtifactGetResponse
        {
            ProjectKey = projectKey,
            Version = artifact.Version.ToString(),
            FileName = artifact.FileName,
            FileSizeBytes = (long?)artifact.FileSizeBytes,
            Sha256Hash = artifact.Sha256Hash,
            Locked = artifact.Locked,
            Retained = artifact.Retained,
            Timestamp = artifact.Timestamp
        };
    }

    public async Task<ActionResult<ICollection<ProjectArtifactsGetResponse>>> GetProjectArtifactsAsync(string projectId, CancellationToken cancellationToken = default)
    {
    _logger.LogInformation("Request to list artifacts for project {ProjectId}", projectId);
        OneOf<List<Artifact>, NotFoundError> response = await _artifactsRepository.GetArtifactsAsync(projectId, cancellationToken);
        if (!response.TryPickT0(out List<Artifact> artifacts, out NotFoundError notFoundError))
        {
            _logger.LogInformation("Project not found {ProjectId}: {Message}", projectId, notFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = notFoundError.Message });
        }

    _logger.LogDebug("Returning {Count} artifacts for project {ProjectId}", artifacts.Count, projectId);

        return artifacts
        .Select(artifact => new ProjectArtifactsGetResponse
        {
            ProjectKey = projectId,
            Version = artifact.Version.ToString(),
            Timestamp = artifact.Timestamp,
            FileName = artifact.FileName,
            FileSizeBytes = (long?)artifact.FileSizeBytes,
            Sha256Hash = artifact.Sha256Hash,
            Retained = artifact.Retained,
            Locked = artifact.Locked
        })
        .ToList();
    }

    public async Task<ActionResult<ArtifactPostResponse>> PostArtifactAsync(string projectKey, string artifactVersion, IFormFile file, CancellationToken cancellationToken = default)
    {
    _logger.LogInformation("Request to create artifact for project {ProjectKey} version {ArtifactVersion} filename {FileName} size {FileLength}", projectKey, artifactVersion, file?.FileName, file?.Length);
        if (!Version.TryParse(artifactVersion, out Version version))
        {
            _logger.LogWarning("Invalid artifact version format for project {ProjectKey} value {ArtifactVersion}", projectKey, artifactVersion);
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Invalid artifact version format." });
        }

        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file uploaded or file is empty for project {ProjectKey} version {Version}", projectKey, version.ToString());
            return new BadRequestObjectResult(new ErrorResponse() { Message = "No file uploaded or file is empty." });
        }

        OneOf<Project, NotFoundError> getProjectResponse = await _projectsRepository.GetProjectAsync(projectKey, cancellationToken);
        if (!getProjectResponse.TryPickT0(out Project project, out NotFoundError notFoundError))
        {
            _logger.LogInformation("Project not found {ProjectKey}: {Message}", projectKey, notFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = notFoundError.Message });
        }

        Artifact newArtifact = new(
            ArtifactId: 0, // 0 or a default value, assuming it will be set by the repository or database
            ProjectId: project.ProjectId,
            Version: version,
            FileName: file.FileName,
            FileSizeBytes: (ulong)file.Length,
            Sha256Hash: string.Empty, // Will be calculated during file storage
            Timestamp: DateTime.UtcNow,
            Retained: false,
            Locked: false
        );

        await using Stream stream = file.OpenReadStream();
        OneOf<Artifact, NotFoundError, ConflictError> response = await _artifactsRepository.NewArtifactAsync(newArtifact, stream, cancellationToken);
        if (!response.TryPickT0(out Artifact createdArtifact, out OneOf<NotFoundError, ConflictError> error))
        {
            return error.Match<ActionResult>(
                notFound =>
                {
                    _logger.LogInformation("Repository reported not found while creating artifact for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), notFound.Message);
                    return new NotFoundObjectResult(new ErrorResponse() { Message = notFound.Message });
                },
                conflict =>
                {
                    _logger.LogWarning("Conflict creating artifact for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), conflict.Message);
                    return new ConflictObjectResult(new ErrorResponse() { Message = conflict.Message });
                }
            );
        }

    _logger.LogInformation("Created artifact for project {ProjectKey} version {Version} filename {FileName} size {FileSize}", projectKey, createdArtifact.Version.ToString(), createdArtifact.FileName, createdArtifact.FileSizeBytes);

        ArtifactPostResponse artifactResponse = new()
        {
            ProjectKey = projectKey,
            Version = createdArtifact.Version.ToString(),
            Timestamp = createdArtifact.Timestamp,
            FileName = createdArtifact.FileName,
            FileSizeBytes = (long?)createdArtifact.FileSizeBytes,
            Sha256Hash = createdArtifact.Sha256Hash,
            Retained = createdArtifact.Retained,
            Locked = createdArtifact.Locked
        };

        return new CreatedAtRouteResult("GetArtifact",
            new { projectKey, artifactVersion = createdArtifact.Version.ToString() },
            artifactResponse);
    }

    public async Task<IActionResult> PutArtifactAsync(ArtifactPutRequest body, string projectKey, string artifactVersion, CancellationToken cancellationToken = default)
    {
    _logger.LogInformation("Request to update artifact for project {ProjectKey} version {ArtifactVersion} bodyVersion {BodyVersion} retained {Retained} locked {Locked}", projectKey, artifactVersion, body?.Version, body?.Retained, body?.Locked);
        if (!Version.TryParse(artifactVersion, out Version version))
        {
            _logger.LogWarning("Invalid artifact version format for project {ProjectKey} value {ArtifactVersion}", projectKey, artifactVersion);
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Invalid artifact version format." });
        }

        OneOf<Artifact, NotFoundError> getResponse = await _artifactsRepository.GetArtifactAsync(projectKey, version, cancellationToken);
        if (!getResponse.TryPickT0(out Artifact artifact, out NotFoundError notFoundError))
        {
            _logger.LogInformation("Artifact not found for project {ProjectKey} version {Version}: {Message}", projectKey, version.ToString(), notFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = notFoundError.Message });
        }

        if (body == null)
        {
            _logger.LogWarning("PutArtifactAsync: request body is null for project {ProjectKey} version {ArtifactVersion}", projectKey, artifactVersion);
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Request body is required." });
        }

        if (!Version.TryParse(body.Version, out Version newVersion))
        {
            _logger.LogWarning("Invalid new version format for project {ProjectKey} value {BodyVersion}", projectKey, body.Version);
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Invalid new version format." });
        }

        Artifact proposedArtifact = artifact with
        {
            Version = newVersion, // Update version from request body
            Retained = body.Retained ?? artifact.Retained,
            Locked = body.Locked ?? artifact.Locked
        };

        OneOf<Success, BadRequestError, NotFoundError, ConflictError> updateResponse = await _artifactsRepository.UpdateArtifactAsync(proposedArtifact, cancellationToken);
        if (!updateResponse.TryPickT0(out Success success, out OneOf<BadRequestError, NotFoundError, ConflictError> remaining))
        {
            // Handle errors
            return remaining.Match<IActionResult>(
                badRequestError =>
                {
                    _logger.LogWarning("Bad request updating artifact for project {ProjectKey} version {Version}: {Message}", projectKey, proposedArtifact.Version.ToString(), badRequestError.Message);
                    return new BadRequestObjectResult(new ErrorResponse { Message = badRequestError.Message });
                },
                notFoundError =>
                {
                    _logger.LogInformation("Not found updating artifact for project {ProjectKey} version {Version}: {Message}", projectKey, proposedArtifact.Version.ToString(), notFoundError.Message);
                    return new NotFoundObjectResult(new ErrorResponse { Message = notFoundError.Message });
                },
                conflictError =>
                {
                    _logger.LogWarning("Conflict updating artifact for project {ProjectKey} version {Version}: {Message}", projectKey, proposedArtifact.Version.ToString(), conflictError.Message);
                    return new ConflictObjectResult(new ErrorResponse { Message = conflictError.Message });
                }
            );
        }

    _logger.LogInformation("Updated artifact for project {ProjectKey} from version {OldVersion} to {NewVersion}", projectKey, artifact.Version.ToString(), proposedArtifact.Version.ToString());

        return new NoContentResult();
    }
}
