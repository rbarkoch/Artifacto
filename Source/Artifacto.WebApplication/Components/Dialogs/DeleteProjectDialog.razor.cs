using System;
using System.Threading.Tasks;

using Artifacto.Client;

using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Dialogs;

/// <summary>
/// Dialog component used to confirm and perform deletion of a project.
/// </summary>
public partial class DeleteProjectDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// The identifier of the project to delete.
    /// </summary>
    [Parameter]
    public required string ProjectId { get; set; }

    /// <summary>
    /// The display name of the project being deleted (for UI confirmation).
    /// </summary>
    [Parameter]
    public required string ProjectName { get; set; }

    /// <summary>
    /// API client used to call project deletion on the backend.
    /// </summary>
    [Inject]
    private ArtifactoClient ArtifactoClient { get; set; } = default!;

    /// <summary>
    /// Indicates whether a delete operation is in progress.
    /// </summary>
    private bool _isDeleting = false;

    /// <summary>
    /// Indicates whether the last operation resulted in an error.
    /// </summary>
    private bool _hasError = false;

    /// <summary>
    /// Error message to display when an operation fails.
    /// </summary>
    private string _errorMessage = string.Empty;

    // Component state (fields declared above with XML documentation)

    /// <summary>
    /// Confirms deletion and calls the API to remove the project.
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
            await ArtifactoClient.Projects.DeleteProjectAsync(ProjectId);

            // Return success to the parent component
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (ApiException apiEx)
        {
            _hasError = true;
            _errorMessage = $"Failed to delete project: {apiEx.Message}";
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
    /// Cancels the dialog without performing any action.
    /// </summary>
    private void Cancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
