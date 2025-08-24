using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

using Artifacto.Client;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Dialogs;

/// <summary>
/// Dialog component that allows editing an artifact's version.
/// </summary>
public partial class EditArtifactDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// Project identifier owning the artifact being edited.
    /// </summary>
    [Parameter]
    public required string ProjectId { get; set; }

    /// <summary>
    /// Current artifact version being edited.
    /// </summary>
    [Parameter]
    public required string CurrentVersion { get; set; }

    /// <summary>
    /// API client used to call artifact update endpoints.
    /// </summary>
    [Inject]
    private ArtifactoClient ArtifactoClient { get; set; } = default!;

    // Form state
    /// <summary>
    /// Backing model instance used for form binding and validation.
    /// </summary>
    private readonly EditArtifactModel _model = new();

    /// <summary>
    /// Reference to the EditForm used in the component markup.
    /// </summary>
    private EditForm _form = default!;

    /// <summary>
    /// Flag indicating whether a submission is currently in progress.
    /// </summary>
    private bool _isSubmitting = false;

    /// <summary>
    /// Flag indicating whether the last operation resulted in an error.
    /// </summary>
    private bool _hasError = false;

    /// <summary>
    /// Error message to display when an operation fails.
    /// </summary>
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Initialize the component and populate the model with the current version.
    /// </summary>
    protected override void OnInitialized()
    {
        _model.CurrentVersion = CurrentVersion;
        _model.NewVersion = CurrentVersion;
    }

    private class EditArtifactModel
    {
        /// <summary>
        /// The currently stored version string for the artifact.
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// The new version to update the artifact to.
        /// </summary>
        [Required(ErrorMessage = "New version is required")]
        [RegularExpression(Artifacto.Models.Version.VersionPattern, ErrorMessage = "Invalid version format.")]
        public string NewVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validates the form and triggers the update flow when valid.
    /// </summary>
    private async Task HandleSubmit()
    {
        if (_form?.EditContext?.Validate() != true)
        {
            return;
        }

        await OnValidSubmit();
    }

    /// <summary>
    /// Called when the form is valid. Sends the update request to the API and closes the dialog on success.
    /// </summary>
    private async Task OnValidSubmit()
    {
        if (_isSubmitting)
        {
            return;
        }

        _isSubmitting = true;
        _hasError = false;
        _errorMessage = string.Empty;

        try
        {
            StateHasChanged();

            // Create the update request
            ArtifactPutRequest request = new()
            {
                Version = _model.NewVersion
            };

            // Call the API to update the artifact
            await ArtifactoClient.Artifacts.PutArtifactAsync(request, ProjectId, CurrentVersion);

            // Close dialog with success result
            MudDialog.Close(DialogResult.Ok(_model.NewVersion));
        }
        catch (ApiException ex)
        {
            _hasError = true;
            _errorMessage = $"Error updating artifact: {ex.Message}";
        }
        catch (Exception ex)
        {
            _hasError = true;
            _errorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Cancels the edit operation and closes the dialog.
    /// </summary>
    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
