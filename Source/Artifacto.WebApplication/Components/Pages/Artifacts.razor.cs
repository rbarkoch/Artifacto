using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Artifacto.Client;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MudBlazor;

using Artifacto.WebApplication.Components.Dialogs;

namespace Artifacto.WebApplication.Components.Pages;

/// <summary>
/// Page component that shows artifacts for a given project and provides operations such as upload, download, edit and delete.
/// </summary>
public partial class Artifacts
{
    private const long MaxUploadSize = 10 * 1024L * 1024 * 1024;
    /// <summary>
    /// Lightweight DTO representing an artifact row in the UI table.
    /// </summary>
    private record ArtifactRow(string Version, DateTime UploadDate, string FileSize, string FileName);

    /// <summary>
    /// The project identifier for which artifacts are displayed.
    /// </summary>
    [Parameter]
    public required string ProjectId { get; set; }

    /// <summary>
    /// API client used to query project and artifact data.
    /// </summary>
    [Inject]
    private ArtifactoClient ArtifactoClient { get; set; } = default!;

    /// <summary>
    /// JavaScript runtime used for client-side downloads and uploads.
    /// </summary>
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    /// Snackbar service for user notifications.
    /// </summary>
    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    /// <summary>
    /// Service used to show modal dialogs (create/edit/delete/upload dialogs).
    /// </summary>
    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    /// <summary>
    /// Navigation manager used for changing pages.
    /// </summary>
    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    // Project and artifact data
    /// <summary>
    /// Display name of the current project.
    /// </summary>
    private string _projectName = "";

    /// <summary>
    /// Description text for the current project.
    /// </summary>
    private string _projectDescription = "";

    /// <summary>
    /// Latest version available for this project.
    /// </summary>
    private string? _latestVersion = null;

    /// <summary>
    /// Latest stable version available for this project.
    /// </summary>
    private string? _latestStableVersion = null;

    /// <summary>
    /// Indicates whether the component is currently loading data.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// Indicates whether the project has any artifacts.
    /// </summary>
    private bool _hasArtifacts = false;

    // List of artifacts displayed in the UI
    /// <summary>
    /// Collection of artifacts shown in the artifacts table.
    /// </summary>
    private List<ArtifactRow> _artifacts = [];

    /// <summary>
    /// Loads project and artifact data when the component initializes.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await LoadProjectData();
    }

    /// <summary>
    /// Loads project metadata and artifact list from the API.
    /// </summary>
    private async Task LoadProjectData()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            // Load project details
            ProjectGetResponse project = await ArtifactoClient.Projects.GetProjectAsync(ProjectId);
            _projectName = project.Name ?? "Unknown Project";
            _projectDescription = project.Description ?? "";
            _latestVersion = project.LatestVersion;
            _latestStableVersion = project.LatestStableVersion;

            // Load artifacts
            ICollection<ProjectArtifactsGetResponse> artifacts = await ArtifactoClient.Artifacts.GetProjectArtifactsAsync(ProjectId);
            _artifacts = [.. artifacts
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new ArtifactRow(
                    a.Version ?? "Unknown",
                    a.Timestamp?.DateTime ?? DateTime.MinValue,
                    FormatFileSize(a.FileSizeBytes ?? 0),
                    a.FileName ?? "Unknown File"
                ))];

            _hasArtifacts = _artifacts.Any();
        }
        catch (ApiException ex)
        {
            Snackbar.Add($"Error loading project: {ex.Message}", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Unexpected error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Shows the upload dialog for adding a new artifact to this project.
    /// </summary>
    private async Task ShowUploadModal()
    {
        DialogParameters parameters = new()
        {
            [nameof(UploadArtifactDialog.ProjectId)] = ProjectId
        };

        DialogOptions options = new()
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

    IDialogReference dialog = await DialogService.ShowAsync<Artifacto.WebApplication.Components.Dialogs.UploadArtifactDialog>("Upload New Artifact", parameters, options);
        DialogResult? result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            if (result.Data is ArtifactPostResponse response)
            {
                Snackbar.Add($"Artifact uploaded successfully!", Severity.Success);
            }
            await LoadProjectData(); // Reload data after upload
        }
    }

    /// <summary>
    /// Initiates download of the latest artifact (most recent by timestamp).
    /// </summary>
    private async Task DownloadLatest()
    {
        if (string.IsNullOrEmpty(_latestVersion))
        {
            Snackbar.Add("No latest version available", Severity.Warning);
            return;
        }

        await DownloadArtifact(_latestVersion);
    }

    /// <summary>
    /// Initiates download of the latest stable artifact.
    /// </summary>
    private async Task DownloadLatestStable()
    {
        if (string.IsNullOrEmpty(_latestStableVersion))
        {
            Snackbar.Add("No stable version available", Severity.Warning);
            return;
        }

        await DownloadArtifact(_latestStableVersion);
    }

    /// <summary>
    /// Starts a browser download for the specified artifact version.
    /// </summary>
    /// <param name="version">Artifact version to download.</param>
    private async Task DownloadArtifact(string version)
    {
        try
        {
            // Use the download controller which provides better browser download progress
            string downloadUrl = $"/projects/{ProjectId}/artifacts/{version}/download";
            await JSRuntime.InvokeVoidAsync("downloadFileFromUrl", $"artifact-{version}", downloadUrl);

            Snackbar.Add($"Download started for artifact {version}", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error starting download: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// Opens a confirmation dialog and deletes the specified artifact when confirmed.
    /// </summary>
    /// <param name="version">Artifact version to delete.</param>
    private async Task DeleteArtifact(string version)
    {
        // Find the artifact to get the filename for the confirmation dialog
        ArtifactRow? artifact = _artifacts.FirstOrDefault(a => a.Version == version);
        string fileName = artifact?.FileName ?? "Unknown File";

        DialogParameters parameters = new()
        {
            [nameof(DeleteArtifactDialog.ProjectId)] = ProjectId,
            [nameof(DeleteArtifactDialog.Version)] = version,
            [nameof(DeleteArtifactDialog.FileName)] = fileName
        };

        DialogOptions options = new()
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

    IDialogReference dialog = await DialogService.ShowAsync<Artifacto.WebApplication.Components.Dialogs.DeleteArtifactDialog>("Delete Artifact", parameters, options);
        DialogResult? result = await dialog.Result;

        if (result is not null && !result.Canceled && result.Data is bool success && success)
        {
            Snackbar.Add($"Artifact {version} deleted successfully!", Severity.Success);
            await LoadProjectData(); // Reload data after deletion
        }
    }

    /// <summary>
    /// Opens the edit artifact dialog for updating the artifact version.
    /// </summary>
    /// <param name="version">Current artifact version to edit.</param>
    private async Task EditArtifact(string version)
    {
        DialogParameters parameters = new()
        {
            [nameof(EditArtifactDialog.ProjectId)] = ProjectId,
            [nameof(EditArtifactDialog.CurrentVersion)] = version
        };

        DialogOptions options = new()
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

    IDialogReference dialog = await DialogService.ShowAsync<Artifacto.WebApplication.Components.Dialogs.EditArtifactDialog>("Edit Artifact Version", parameters, options);
        DialogResult? result = await dialog.Result;

        if (result is not null && !result.Canceled && result.Data is string newVersion)
        {
            Snackbar.Add($"Artifact version updated from {version} to {newVersion}", Severity.Success);
            await LoadProjectData(); // Reload data after update
        }
    }

    /// <summary>
    /// Navigates back to the projects list page.
    /// </summary>
    private void NavigateToProjects()
    {
        NavigationManager.NavigateTo("/projects");
    }

    /// <summary>
    /// Opens the edit project dialog and updates or navigates after success.
    /// </summary>
    private async Task EditProject()
    {
        DialogParameters parameters = new()
        {
            [nameof(EditProjectDialog.ProjectId)] = ProjectId,
            [nameof(EditProjectDialog.ProjectName)] = _projectName,
            [nameof(EditProjectDialog.ProjectDescription)] = _projectDescription
        };

        DialogOptions options = new()
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

    IDialogReference dialog = await DialogService.ShowAsync<Artifacto.WebApplication.Components.Dialogs.EditProjectDialog>("Edit Project", parameters, options);
        DialogResult? result = await dialog.Result;

        if (result is not null && !result.Canceled && result.Data is string newProjectId)
        {
            Snackbar.Add("Project updated successfully!", Severity.Success);

            // If the project ID changed, navigate to the new URL
            if (newProjectId != ProjectId)
            {
                NavigationManager.NavigateTo($"/projects/{newProjectId}/artifacts");
            }
            else
            {
                // If only name/description changed, reload the current page data
                await LoadProjectData();
            }
        }
    }

    /// <summary>
    /// Opens the delete project dialog and navigates away when the project is removed.
    /// </summary>
    private async Task DeleteProject()
    {
        DialogParameters parameters = new()
        {
            [nameof(DeleteProjectDialog.ProjectId)] = ProjectId,
            [nameof(DeleteProjectDialog.ProjectName)] = _projectName
        };

        DialogOptions options = new()
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

    IDialogReference dialog = await DialogService.ShowAsync<Artifacto.WebApplication.Components.Dialogs.DeleteProjectDialog>("Delete Project", parameters, options);
        DialogResult? result = await dialog.Result;

        if (result is not null && !result.Canceled && result.Data is bool success && success)
        {
            Snackbar.Add($"Project '{_projectName}' deleted successfully!", Severity.Success);
            NavigationManager.NavigateTo("/projects"); // Navigate back to projects list
        }
    }

    /// <summary>
    /// Converts a byte count into a human-readable file size string.
    /// </summary>
    /// <param name="bytes">The number of bytes.</param>
    /// <returns>The formatted size string.</returns>
    private static string FormatFileSize(long bytes)
    {
        const long scale = 1024;
        string[] orders = ["B", "KB", "MB", "GB", "TB"];

        double size = bytes;
        int order = 0;

        while (size >= scale && order < orders.Length - 1)
        {
            order++;
            size /= scale;
        }

        return $"{size:0.##} {orders[order]}";
    }
}
