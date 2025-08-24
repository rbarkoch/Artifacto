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
/// Dialog component for editing project metadata such as name and description.
/// </summary>
public partial class EditProjectDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// The key/identifier of the project being edited.
    /// </summary>
    [Parameter]
    public required string ProjectId { get; set; }

    /// <summary>
    /// The current display name of the project being edited.
    /// </summary>
    [Parameter]
    public required string ProjectName { get; set; }

    /// <summary>
    /// The current description of the project being edited.
    /// </summary>
    [Parameter]
    public required string ProjectDescription { get; set; }

    /// <summary>
    /// API client used to call the Artifacto backend for project operations.
    /// </summary>
    [Inject]
    private ArtifactoClient ArtifactoClient { get; set; } = default!;

    // Backing model for the edit form
    /// <summary>
    /// Backing model instance used for form binding and validation.
    /// </summary>
    private readonly EditProjectModel _model = new();

    // References and state used by the edit form
    /// <summary>
    /// Reference to the EditForm used in the component markup.
    /// </summary>
    private EditForm _form = default!;

    /// <summary>
    /// The EditContext used for validating the edit form.
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
    /// Initialize the edit model from the incoming parameters.
    /// </summary>
    protected override void OnInitialized()
    {
        _model.Id = ProjectId;
        _model.Name = ProjectName;
        _model.Description = ProjectDescription;
        _editContext = new EditContext(_model);
    }

    private class EditProjectModel
    {
    /// <summary>
    /// The project key. Must match <see cref="Project.ProjectKeyPattern"/>.
    /// </summary>
    [Required(ErrorMessage = "Project ID is required")]
    [RegularExpression(Project.ProjectKeyPattern, ErrorMessage = "Project ID must contain only lowercase letters, numbers, and hyphens")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display name for the project.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional descriptive text for the project.
    /// </summary>
    public string? Description { get; set; }
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
            ProjectPutRequest request = new()
            {
                Key = _model.Id,
                Name = _model.Name,
                Description = _model.Description
            };

            await ArtifactoClient.Projects.PutProjectAsync(request, ProjectId);

            // Return the updated project ID to the parent component
            MudDialog.Close(DialogResult.Ok(_model.Id));
        }
        catch (ApiException apiEx)
        {
            _hasError = true;
            _errorMessage = $"Failed to update project: {apiEx.Message}";
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
    /// Validates the edit form and triggers submission.
    /// </summary>
    private async Task HandleSubmit()
    {
        if (_editContext.Validate())
        {
            await OnValidSubmit();
        }
    }

    /// <summary>
    /// Cancels the dialog without saving changes.
    /// </summary>
    private void Cancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }
}
