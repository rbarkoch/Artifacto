using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Artifacto.Client;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactsController"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="artifactoClient">The Artifacto client for API operations.</param>
    public ArtifactsController(ILogger<ArtifactsController> logger, ArtifactoClient artifactoClient, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _artifactoClient = artifactoClient;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    private static string ExtractDownloadFileName(FileResponse fileResponse, string fallbackFileName)
    {
        if (fileResponse.Headers.TryGetValue("Content-Disposition", out System.Collections.Generic.IEnumerable<string>? dispositionValues))
        {
            string? disposition = dispositionValues?.FirstOrDefault();
            if (!string.IsNullOrEmpty(disposition) && disposition.Contains("filename="))
            {
                string fileNamePart = disposition.Split("filename=")[1].Trim('"');
                if (!string.IsNullOrEmpty(fileNamePart))
                {
                    return fileNamePart;
                }
            }
        }

        return fallbackFileName;
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
            string fileName = ExtractDownloadFileName(fileResponse, $"artifact-{version}");

            // Set the Content-Disposition header to suggest the filename
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

            // Return the file stream directly - this allows the browser to show download progress
            return File(fileResponse.Stream, "application/octet-stream", fileName);
        }
        catch (ApiException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads the SBOM for the given project and artifact version.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="version">The artifact version.</param>
    /// <param name="format">The requested download format.</param>
    /// <param name="specVersion">The requested CycloneDX spec version.</param>
    /// <returns>An <see cref="IActionResult"/> containing the generated SBOM stream or an error status.</returns>
    [HttpGet("projects/{projectId}/artifacts/{version}/sbom")]
    public async Task<IActionResult> DownloadArtifactSbom(string projectId, string version, [FromQuery] string? format = "json", [FromQuery] string? specVersion = "1.7")
    {
        string normalizedFormat = string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase) ? "xml" : "json";
        if (!string.IsNullOrWhiteSpace(format) &&
            !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid SBOM format.");
        }

        if (!string.IsNullOrWhiteSpace(specVersion) &&
            specVersion != "1.3" &&
            specVersion != "1.4" &&
            specVersion != "1.5" &&
            specVersion != "1.6" &&
            specVersion != "1.7")
        {
            return BadRequest("Invalid CycloneDX specification version.");
        }

        try
        {
            string baseUrl = _configuration["ArtifactoApi:BaseUrl"] ?? "https://localhost:7001";
            Uri requestUri = new($"{baseUrl.TrimEnd('/')}/projects/{Uri.EscapeDataString(projectId)}/artifacts/{Uri.EscapeDataString(version)}/sbom?format={Uri.EscapeDataString(normalizedFormat)}&specVersion={Uri.EscapeDataString(specVersion ?? "1.7")}");
            HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpRequestMessage requestMessage = new(HttpMethod.Get, requestUri);
            requestMessage.Headers.Accept.ParseAdd(normalizedFormat == "xml" ? "application/xml" : "application/json");

            using HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
            string responseBody = await responseMessage.Content.ReadAsStringAsync();
            if (!responseMessage.IsSuccessStatusCode)
            {
                return StatusCode((int)responseMessage.StatusCode, responseBody);
            }

            string contentType = normalizedFormat == "xml" ? "application/xml; charset=utf-8" : "application/json; charset=utf-8";

            return Content(responseBody, contentType);
        }
        catch (ApiException ex)
        {
            return StatusCode(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, $"Internal server error: {ex.Message}");
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

    /// <summary>
    /// Uploads or replaces the SBOM for the specified artifact.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="version">The artifact version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An <see cref="IActionResult"/> with the upload result or an error status.</returns>
    [HttpPut("projects/{projectId}/artifacts/{version}/sbom")]
    public async Task<IActionResult> UploadArtifactSbom(
        string projectId,
        string version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (Request.ContentLength == 0)
            {
                _logger.LogWarning("SBOM upload request body is empty. Content-Type: {ContentType}", Request.ContentType);
                return BadRequest("Request body must contain SBOM content");
            }

            string contentType = string.IsNullOrWhiteSpace(Request.ContentType) ? "application/octet-stream" : Request.ContentType;
            FileParameter fileParameter = new(Request.Body, "sbom", contentType);
            await _artifactoClient.Artifacts.PutArtifactSbomAsync(fileParameter, projectId, version, cancellationToken);
            return NoContent();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SBOM upload was cancelled for project {ProjectId} version {Version}", projectId, version);
            return StatusCode(StatusCodes.Status499ClientClosedRequest, "Upload was cancelled");
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error during SBOM upload for project {ProjectId} version {Version}. Status: {StatusCode}, Response: {Response}", projectId, version, ex.StatusCode, ex.Response);
            return StatusCode(ex.StatusCode, $"API error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SBOM upload for project {ProjectId} version {Version}", projectId, version);
            return StatusCode(StatusCodes.Status500InternalServerError, $"Internal server error: {ex.Message}");
        }
    }
}
