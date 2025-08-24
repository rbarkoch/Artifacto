using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Artifacto.Client;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Artifacto.WebApplication.Controllers;

[ApiController]
/// <summary>
/// Controller for handling artifact operations.
/// Provides endpoints for uploading and downloading artifacts from the Artifacto system.
/// </summary>
public class ArtifactsController : ControllerBase
{
    private readonly ILogger<ArtifactsController> _logger;
    private readonly ArtifactoClient _artifactoClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactsController"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="artifactoClient">The Artifacto client for API operations.</param>
    public ArtifactsController(ILogger<ArtifactsController> logger, ArtifactoClient artifactoClient)
    {
        _logger = logger;
        _artifactoClient = artifactoClient;
    }

    /// <summary>
    /// Downloads an artifact for the given project and version and returns it as a file result.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="version">The artifact version.</param>
    /// <returns>An <see cref="IActionResult"/> containing the artifact stream or an error status.</returns>
    [HttpGet("projects/{projectId}/artifacts/{version}/download")]
    public async Task<IActionResult> DownloadArtifact(string projectId, string version)
    {
        try
        {
            FileResponse fileResponse = await _artifactoClient.Artifacts.DownloadArtifactAsync(projectId, version);

            // Extract filename from the API response headers if available
            string fileName = $"artifact-{version}";
            if (fileResponse.Headers.TryGetValue("Content-Disposition", out System.Collections.Generic.IEnumerable<string>? dispositionValues))
            {
                string? disposition = dispositionValues?.FirstOrDefault();
                if (!string.IsNullOrEmpty(disposition) && disposition.Contains("filename="))
                {
                    string fileNamePart = disposition.Split("filename=")[1].Trim('"');
                    if (!string.IsNullOrEmpty(fileNamePart))
                    {
                        fileName = fileNamePart;
                    }
                }
            }

            // Set the Content-Disposition header to suggest the filename
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

            // Return the file stream directly - this allows the browser to show download progress
            return File(fileResponse.Stream, "application/octet-stream", fileName);
        }
        catch (ApiException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
        catch (System.Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Uploads an artifact to the specified project and version.
    /// This endpoint streams the incoming request body to avoid buffering large files in memory.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="version">The artifact version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> with the upload result or an error status.</returns>
    [HttpPost("projects/{projectId}/artifacts/{version}/upload")]
    public async Task<IActionResult> UploadArtifact(
        string projectId,
        string version,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== ARTIFACTS CONTROLLER HIT === Project: {ProjectId}, Version: {Version}", projectId, version);
        
        try
        {
            _logger.LogInformation("Upload request for project {ProjectId} version {Version}", projectId, version);

            // Read the uploaded file
            if (!Request.HasFormContentType)
            {
                _logger.LogWarning("Request is not multipart/form-data. Content-Type: {ContentType}", Request.ContentType);
                return BadRequest("Request must be multipart/form-data");
            }

            IFormCollection form = await Request.ReadFormAsync(cancellationToken);
            IFormFile? uploadedFile = form.Files.GetFile("file");
            
            if (uploadedFile == null)
            {
                _logger.LogWarning("No file found in form data. Available files: {FileNames}", string.Join(", ", form.Files.Select(f => f.Name)));
                return BadRequest("No file found in form data");
            }

            _logger.LogInformation("File received: Name={FileName}, Size={FileSize}, ContentType={ContentType}", 
                uploadedFile.FileName, uploadedFile.Length, uploadedFile.ContentType);

            // Convert IFormFile to FileParameter for the client
            FileParameter fileParameter = new(uploadedFile.OpenReadStream(), uploadedFile.FileName, uploadedFile.ContentType);

            _logger.LogInformation("Calling ArtifactoClient.Artifacts.PostArtifactAsync...");

            // Use the ArtifactoClient to upload the artifact
            ArtifactPostResponse response = await _artifactoClient.Artifacts.PostArtifactAsync(
                projectId,
                version,
                fileParameter,
                cancellationToken
            );

            _logger.LogInformation("Upload successful for project {ProjectId} version {Version}", projectId, version);

            // Return the response as JSON
            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Upload was cancelled for project {ProjectId} version {Version}", projectId, version);
            return StatusCode(StatusCodes.Status499ClientClosedRequest, "Upload was cancelled");
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error during upload for project {ProjectId} version {Version}. Status: {StatusCode}, Response: {Response}", 
                projectId, version, ex.StatusCode, ex.Response);
            return StatusCode(ex.StatusCode, $"API error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during upload for project {ProjectId} version {Version}", projectId, version);
            return StatusCode(StatusCodes.Status500InternalServerError, $"Internal server error: {ex.Message}");
        }
    }
}
