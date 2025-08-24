using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Artifacto.Models;
using Artifacto.Repository;

using Microsoft.AspNetCore.Mvc;

using OneOf;

using OneOf.Types;
using Microsoft.Extensions.Logging;

namespace Artifacto.WebApi.ControllerImplementations;

/// <summary>
/// Implementation of <see cref="IProjectsController"/> for managing projects.
/// </summary>
public class ProjectsControllerImplementation : IProjectsController
{
    private readonly ProjectsRepository _projectsRepository;
    private readonly ILogger<ProjectsControllerImplementation> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectsControllerImplementation"/> class.
    /// </summary>
    /// <param name="projectsRepository">The projects repository.</param>
    public ProjectsControllerImplementation(ILogger<ProjectsControllerImplementation> logger, ProjectsRepository projectsRepository)
    {
        _projectsRepository = projectsRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IActionResult> DeleteProjectAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Request to delete project {ProjectKey}", projectKey);
        OneOf<Success, NotFoundError> response = await _projectsRepository.DeleteProjectAsync(projectKey, cancellationToken);
        if (!response.TryPickT0(out Success _, out NotFoundError notFoundError))
        {
            _logger.LogInformation("Project not found {ProjectKey}: {Message}", projectKey, notFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = notFoundError.Message });
        }

        _logger.LogInformation("Deleted project {ProjectKey}", projectKey);
        return new NoContentResult();
    }

    /// <inheritdoc />
    public async Task<ActionResult<ProjectGetResponse>> GetProjectAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Request to get project {ProjectKey}", projectKey);
        OneOf<Project, NotFoundError> response = await _projectsRepository.GetProjectAsync(projectKey, cancellationToken);
        if (!response.TryPickT0(out Project project, out NotFoundError notFoundError))
        {
            _logger.LogInformation("Project not found {ProjectKey}: {Message}", projectKey, notFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse() { Message = notFoundError.Message });
        }

        _logger.LogDebug("Found project {ProjectKey} name {Name}", projectKey, project.Name);
        return new ProjectGetResponse
        {
            Key = project.Key,
            Name = project.Name,
            Description = project.Description,
            LatestStableVersion = project.LatestStableVersion,
            LatestVersion = project.LatestVersion,
            LatestStableVersionUploadDate = project.LatestStableVersionUploadDate,
            LatestVersionUploadDate = project.LatestVersionUploadDate
        };
    }

    /// <inheritdoc />
    public async Task<ActionResult<ICollection<ProjectsGetResponse>>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Request to list all projects");
        IEnumerable<Project> projects = await _projectsRepository.GetProjectsAsync(cancellationToken);
        List<ProjectsGetResponse> result = [.. projects.Select(project => new ProjectsGetResponse
        {
            Key = project.Key,
            Name = project.Name,
            Description = project.Description,
            ArtifactCount = project.ArtifactCount,
            LatestStableVersion = project.LatestStableVersion,
            LatestVersion = project.LatestVersion,
            LatestStableVersionUploadDate = project.LatestStableVersionUploadDate,
            LatestVersionUploadDate = project.LatestVersionUploadDate
        })];
        _logger.LogDebug("Returning {Count} projects", result.Count);
        return result;
    }

    /// <inheritdoc />
    public async Task<ActionResult<ProjectPostResponse>> PostProjectAsync(ProjectPostRequest body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Request to create project with key {ProjectKey} name {Name}", body?.Key, body?.Name);
        if (body == null)
        {
            _logger.LogWarning("Request body is null for project creation");
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Request body is required." });
        }
        Project newProject = new(
            ProjectId: 0, // ProjectId will be set by the database
            Key: body.Key,
            Name: body.Name,
            Description: body.Description
        );

        OneOf<Project, ConflictError> response = await _projectsRepository.NewProjectAsync(newProject, cancellationToken);
        if (!response.TryPickT0(out Project createdProject, out ConflictError conflictError))
        {
            _logger.LogWarning("Conflict creating project {ProjectKey}: {Message}", body.Key, conflictError.Message);
            return new ConflictObjectResult(new ErrorResponse() { Message = conflictError.Message });
        }

        _logger.LogInformation("Created project {ProjectKey} name {Name}", createdProject.Key, createdProject.Name);
        ProjectPostResponse projectResponse = new()
        {
            Key = createdProject.Key,
            Name = createdProject.Name,
            Description = createdProject.Description
        };

        return new CreatedAtRouteResult("GetProject", new { projectKey = createdProject.Key }, projectResponse);
    }

    /// <inheritdoc />
    public async Task<IActionResult> PutProjectAsync(ProjectPutRequest body, string projectKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Request to update project {ProjectKey} name {Name} description {Description}", projectKey, body?.Name, body?.Description);
        if (body == null)
        {
            _logger.LogWarning("Request body is null for project update {ProjectKey}", projectKey);
            return new BadRequestObjectResult(new ErrorResponse() { Message = "Request body is required." });
        }
        OneOf<Project, NotFoundError> getResponse = await _projectsRepository.GetProjectAsync(projectKey, cancellationToken);
        if (!getResponse.TryPickT0(out Project project, out NotFoundError notFoundError))
        {
            _logger.LogInformation("Project not found {ProjectKey}: {Message}", projectKey, notFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse { Message = notFoundError.Message });
        }

        Project proposedProject = project with
        {
            Key = body.Key ?? project.Key,
            Name = body.Name ?? project.Name,
            Description = body.Description ?? project.Description
        };

        OneOf<Success, BadRequestError, NotFoundError, ConflictError> updateResponse = await _projectsRepository.UpdateProjectAsync(proposedProject, cancellationToken);
        if (!updateResponse.TryPickT0(out Success _, out OneOf<BadRequestError, NotFoundError, ConflictError> errors))
        {
            return errors.Match<IActionResult>(
                badRequestError =>
                {
                    _logger.LogWarning("Bad request updating project {ProjectKey}: {Message}", projectKey, badRequestError.Message);
                    return new BadRequestObjectResult(new ErrorResponse { Message = badRequestError.Message });
                },
                notFoundError =>
                {
                    _logger.LogInformation("Project not found updating {ProjectKey}: {Message}", projectKey, notFoundError.Message);
                    return new NotFoundObjectResult(new ErrorResponse { Message = notFoundError.Message });
                },
                conflictError =>
                {
                    _logger.LogWarning("Conflict updating project {ProjectKey}: {Message}", projectKey, conflictError.Message);
                    return new ConflictObjectResult(new ErrorResponse { Message = conflictError.Message });
                }
            );
        }

        // Get the updated project with latest version information
        OneOf<Project, NotFoundError> updatedProjectResponse = await _projectsRepository.GetProjectAsync(proposedProject.Key, cancellationToken);
        if (!updatedProjectResponse.TryPickT0(out Project updatedProject, out NotFoundError updatedNotFoundError))
        {
            _logger.LogWarning("Project not found after update {ProjectKey}: {Message}", proposedProject.Key, updatedNotFoundError.Message);
            return new NotFoundObjectResult(new ErrorResponse { Message = updatedNotFoundError.Message });
        }

        _logger.LogInformation("Updated project {ProjectKey} name {Name}", updatedProject.Key, updatedProject.Name);
        return new OkObjectResult(new ProjectGetResponse
        {
            Key = updatedProject.Key,
            Name = updatedProject.Name,
            Description = updatedProject.Description,
            LatestStableVersion = updatedProject.LatestStableVersion,
            LatestVersion = updatedProject.LatestVersion,
            LatestStableVersionUploadDate = updatedProject.LatestStableVersionUploadDate,
            LatestVersionUploadDate = updatedProject.LatestVersionUploadDate
        });
    }
}
