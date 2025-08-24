using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

using Artifacto.Client;
using Artifacto.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Dialogs;

/// <summary>
/// Dialog component used to create a new project using the Artifacto API.
/// </summary>
public partial class CreateProjectDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    private ArtifactoClient ArtifactoClient { get; set; } = default!;

    /// <summary>
    /// Backing model instance used for the create project form.
    /// </summary>
    private readonly CreateProjectModel _model = new();

    /// <summary>
    /// Reference to the EditForm used in the create dialog.
    /// </summary>
    private EditForm _form = default!;

    /// <summary>
    /// EditContext for validating the create project form.
    /// </summary>
    private EditContext _editContext = default!;

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
    /// Initializes the create project form and validation context.
    /// </summary>
    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);
    }

    private class CreateProjectModel
    {
        [Required(ErrorMessage = "Project ID is required")]
        [RegularExpression(Project.ProjectKeyPattern, ErrorMessage = "Project ID must contain only lowercase letters, numbers, and hyphens")]
        public string Id { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? Description { get; set; }
    }

    /// <summary>
    /// Called when the create project form is valid. Sends the create request and closes the dialog on success.
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
            ProjectPostRequest request = new()
            {
                Key = _model.Id,
                Name = _model.Name,
                Description = _model.Description
            };

            ProjectPostResponse response = await ArtifactoClient.Projects.PostProjectAsync(request);

            // Debug: Check if response is valid
            Console.WriteLine($"Response received: Key={response?.Key}, Name={response?.Name}, Description={response?.Description}");

            // Return the created project to the parent component
            MudDialog.Close(DialogResult.Ok(response));
        }
        catch (ApiException apiEx)
        {
            _hasError = true;
            _errorMessage = $"Failed to create project: {apiEx.Message}";
        }
        catch (Exception ex)
        {
            _hasError = true;
            _errorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    /// <summary>
    /// Validates the form and triggers the creation flow when valid.
    /// </summary>
    private async Task HandleSubmit()
    {
        if (_editContext.Validate())
        {
            await OnValidSubmit();
        }
    }

    /// <summary>
    /// Cancels project creation and closes the dialog.
    /// </summary>
    private void Cancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
