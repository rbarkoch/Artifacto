using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Artifacto.Client;

using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Pages;

/// <summary>
/// Page component that lists projects and allows creating, navigating to, editing and deleting projects.
/// </summary>
public partial class Projects
{
    /// <summary>
    /// Lightweight DTO used to render project cards in the projects list UI, including latest version and upload dates.
    /// </summary>
    private record ProjectCard(
        string Id, 
        string Name, 
        string Description, 
        int ArtifactCount, 
        string? LatestVersion,
        string? LatestStableVersion,
        DateTime? LatestVersionUploadDate,
        DateTime? LatestStableVersionUploadDate
    );

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ArtifactoClient ArtifactoClient { get; set; } = default!;

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    /// <summary>
    /// Collection of all project cards loaded from the API.
    /// </summary>
    private List<ProjectCard> _projects = [];

    /// <summary>
    /// The current search term for filtering projects.
    /// </summary>
    private string _searchTerm = string.Empty;

    /// <summary>
    /// Collection of filtered project cards based on the search term.
    /// </summary>
    private List<ProjectCard> FilteredProjects => 
        string.IsNullOrWhiteSpace(_searchTerm) 
            ? _projects 
            : [.. _projects.Where(p => p.Name.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Indicates whether the projects are currently being loaded.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// Loads the projects list from the API when the component is initialized.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            ICollection<ProjectsGetResponse> projects = await ArtifactoClient.Projects.GetProjectsAsync();
            _projects = [.. projects.Select(p => new ProjectCard(
                p.Key ?? string.Empty,
                p.Name ?? string.Empty,
                p.Description ?? string.Empty,
                p.ArtifactCount ?? 0,
                p.LatestVersion ?? null,
                p.LatestStableVersion ?? null,
                p.LatestVersionUploadDate?.DateTime,
                p.LatestStableVersionUploadDate?.DateTime
            ))];
        }
        catch (Exception ex)
        {
            // Log error or handle as appropriate for your application
            // For now, we'll just leave the projects list empty
            Console.WriteLine($"Error loading projects: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Navigates to the project's artifacts page.
    /// </summary>
    /// <param name="projectId">Project identifier to navigate to.</param>
    private void NavigateToProject(string projectId)
    {
        NavigationManager.NavigateTo($"/projects/{projectId}/artifacts");
    }

    /// <summary>
    /// Handles changes to the search term after the debounce interval has elapsed.
    /// </summary>
    /// <param name="searchTerm">The debounced search term.</param>
    private void OnSearchTermChanged(string searchTerm)
    {
        _searchTerm = searchTerm ?? string.Empty;
        StateHasChanged();
    }

    /// <summary>
    /// Opens the create project dialog and inserts the created project into the list on success.
    /// </summary>
    private async Task OpenCreateProjectDialog()
    {
        DialogParameters parameters = [];
        DialogOptions options = new()
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

    IDialogReference dialog = await DialogService.ShowAsync<Artifacto.WebApplication.Components.Dialogs.CreateProjectDialog>("Create Project", parameters, options);
        DialogResult? result = await dialog.Result;
    if (result is null || result.Canceled || result.Data is not ProjectPostResponse newProject)
        {
            return; // Exit if dialog was canceled or result is not as expected
        }

        // Add the new project to the list and refresh the UI
        ProjectCard newProjectCard = new(
            newProject.Key ?? string.Empty,
            newProject.Name ?? string.Empty,
            newProject.Description ?? string.Empty,
            0, // New projects start with 0 artifacts
            null, // No version for new project
            null, // No stable version for new project
            null, // No upload date for new project
            null  // No stable upload date for new project
        );

        _projects.Add(newProjectCard);
        StateHasChanged();
    }
}
