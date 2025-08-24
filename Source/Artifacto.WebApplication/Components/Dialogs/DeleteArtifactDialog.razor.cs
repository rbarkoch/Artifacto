using System;
using System.Threading.Tasks;

using Artifacto.Client;

using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Dialogs;

/// <summary>
/// Dialog component used to confirm and perform deletion of a specific artifact version.
/// </summary>
public partial class DeleteArtifactDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// The project identifier that owns the artifact to delete.
    /// </summary>
    [Parameter]
    public required string ProjectId { get; set; }

    /// <summary>
    /// The artifact version to delete.
    /// </summary>
    [Parameter]
    public required string Version { get; set; }

    /// <summary>
    /// The human-friendly filename of the artifact (used for display in the dialog).
    /// </summary>
    [Parameter]
    public required string FileName { get; set; }

    /// <summary>
    /// The API client used to call artifact-related endpoints.
    /// </summary>
    [Inject]
    private ArtifactoClient ArtifactoClient { get; set; } = default!;

    // Component state
    private bool _isDeleting = false;
    private bool _hasError = false;
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Confirms deletion and calls the API to remove the artifact.
    /// Closes the dialog with <see cref="DialogResult.Ok"/> on success.
    /// </summary>
    private async Task ConfirmDelete()
    {
        if (_isDeleting)
        {
            return;
        }

        _isDeleting = true;
        _hasError = false;
        _errorMessage = string.Empty;

        try
        {
            await ArtifactoClient.Artifacts.DeleteArtifactAsync(ProjectId, Version);

            // Return success to the parent component
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (ApiException apiEx)
        {
            _hasError = true;
            _errorMessage = $"Failed to delete artifact: {apiEx.Message}";
        }
        catch (Exception ex)
        {
            _hasError = true;
            _errorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            _isDeleting = false;
        }
    }

    /// <summary>
    /// Cancels the dialog without performing deletion.
    /// </summary>
    private void Cancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
