using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Tasks;

using Artifacto.Client;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Dialogs;

/// <summary>
/// Dialog component that handles uploading an artifact file to a project.
/// Manages upload progress via JavaScript interop and reports results to the caller.
/// </summary>
public partial class UploadArtifactDialog : ComponentBase, IDisposable
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// The project identifier to which the selected artifact will be uploaded.
    /// </summary>
    [Parameter]
    public required string ProjectId { get; set; }

    /// <summary>
    /// JavaScript runtime for performing the browser-based upload and callbacks.
    /// </summary>
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    /// Navigation manager used to construct upload URLs relative to the app base URI.
    /// </summary>
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    // Form model and UI state
    /// <summary>
    /// Backing model used for the upload form validation.
    /// </summary>
    private readonly UploadArtifactModel _model = new();

    /// <summary>
    /// Reference to the EditForm used for the upload dialog.
    /// </summary>
    private EditForm _form = default!;

    /// <summary>
    /// EditContext used for validating the upload form.
    /// </summary>
    private EditContext _editContext = default!;

    /// <summary>
    /// Flag indicating whether an upload is currently in progress.
    /// </summary>
    private bool _isUploading = false;

    /// <summary>
    /// Flag indicating whether the last operation resulted in an error.
    /// </summary>
    private bool _hasError = false;

    /// <summary>
    /// Error message to display when an operation fails.
    /// </summary>
    private string _errorMessage = string.Empty;

    /// <summary>
    /// The browser file selected by the user for upload.
    /// </summary>
    private IBrowserFile? _selectedFile = null;

    /// <summary>
    /// Upload progress percentage (0-100).
    /// </summary>
    private double _uploadProgress = 0;

    /// <summary>
    /// Human readable upload status text.
    /// </summary>
    private string _uploadStatus = string.Empty;

    /// <summary>
    /// Number of bytes uploaded so far.
    /// </summary>
    private long _bytesUploaded = 0;

    /// <summary>
    /// Total number of bytes to upload.
    /// </summary>
    private long _totalBytes = 0;

    /// <summary>
    /// DotNet object reference used to provide JS callbacks to this component.
    /// </summary>
    private DotNetObjectReference<UploadArtifactDialog>? _dotNetRef = null;

    /// <summary>
    /// Current upload identifier returned by the JS upload helper.
    /// </summary>
    private string? _currentUploadId = null;

    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);
        _dotNetRef = DotNetObjectReference.Create(this);
    }

    /// <summary>
    /// Form model used for validating the upload version field.
    /// </summary>
    private class UploadArtifactModel
    {
        [Required(ErrorMessage = "Version is required")]
        [RegularExpression(Artifacto.Models.Version.VersionPattern, ErrorMessage = "Invalid version format.")]
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handler invoked when the user selects a file in the file input control.
    /// </summary>
    /// <param name="e">Event args containing the selected file.</param>
    private void OnFileSelected(InputFileChangeEventArgs e)
    {
        _selectedFile = e.File;
        StateHasChanged();
    }

    /// <summary>
    /// Called when the upload form is valid and initiates the JavaScript-driven file upload.
    /// </summary>
    private async Task OnValidSubmit()
    {
        if (_isUploading || _selectedFile == null)
        {
            return;
        }

        _isUploading = true;
        _hasError = false;
        _errorMessage = string.Empty;
        _uploadProgress = 0;
        _uploadStatus = "Preparing upload...";
        _bytesUploaded = 0;
        _totalBytes = _selectedFile.Size;

        try
        {
            StateHasChanged();

            // Check if artifactoUpload is available
            bool isAvailable = await JSRuntime.InvokeAsync<bool>("eval", "typeof artifactoUpload !== 'undefined'");
            if (!isAvailable)
            {
                throw new InvalidOperationException("Upload module not loaded");
            }

            // Construct the upload URL
            string uploadUrl = $"{Navigation.BaseUri}projects/{Uri.EscapeDataString(ProjectId)}/artifacts/{Uri.EscapeDataString(_model.Version)}/upload";

            // Start the upload using JavaScript interop
            _currentUploadId = await JSRuntime.InvokeAsync<string>(
                "artifactoUpload.uploadFile",
                "fileInput",
                uploadUrl,
                _dotNetRef,
                nameof(OnUploadProgress),
                nameof(OnUploadComplete),
                nameof(OnUploadError)
            );
        }
        catch (Exception ex)
        {
            _hasError = true;
            _errorMessage = $"Upload failed: {ex.Message}";
            _isUploading = false;
            _currentUploadId = null;
            StateHasChanged();
        }
    }

    /// <summary>
    /// JS-invokable callback to report upload progress from the browser.
    /// </summary>
    /// <param name="bytesUploaded">Bytes uploaded so far.</param>
    /// <param name="totalBytes">Total bytes to upload.</param>
    /// <param name="percentComplete">Percent complete (0-100).</param>
    [JSInvokable]
    public Task OnUploadProgress(long bytesUploaded, long totalBytes, double percentComplete)
    {
        _bytesUploaded = bytesUploaded;
        _totalBytes = totalBytes;
        _uploadProgress = percentComplete;
        _uploadStatus = $"Uploading... {FormatFileSize(_bytesUploaded)} / {FormatFileSize(_totalBytes)}";
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// JS-invokable callback invoked when the upload completes. Closes the dialog with the result.
    /// </summary>
    /// <param name="response">The server response object.</param>
    [JSInvokable]
    public async Task OnUploadComplete(object response)
    {
        _uploadProgress = 100;
        _uploadStatus = "Upload complete!";
        _isUploading = false;
        _currentUploadId = null;
        StateHasChanged();

        // Small delay to show completion before closing
        await Task.Delay(500);

        // Try to deserialize the response to the proper type
        ArtifactPostResponse? artifactResponse = null;
        try
        {
            if (response is JsonElement jsonElement)
            {
                string jsonString = jsonElement.GetRawText();
                artifactResponse = JsonSerializer.Deserialize<ArtifactPostResponse>(jsonString, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }
            else if (response != null)
            {
                // If it's already the right type or can be converted
                string jsonString = JsonSerializer.Serialize(response);
                artifactResponse = JsonSerializer.Deserialize<ArtifactPostResponse>(jsonString, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }
        }
        catch
        {
            // If deserialization fails, just use the original response
        }

        // Close dialog with success result
        MudDialog.Close(DialogResult.Ok(artifactResponse ?? response));
    }

    /// <summary>
    /// JS-invokable callback invoked when an upload error occurs.
    /// </summary>
    /// <param name="errorMessage">The error message reported by JavaScript.</param>
    [JSInvokable]
    public Task OnUploadError(string errorMessage)
    {
        _hasError = true;
        _errorMessage = errorMessage;
        _uploadProgress = 0;
        _uploadStatus = string.Empty;
        _bytesUploaded = 0;
        _isUploading = false;
        _currentUploadId = null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the form and triggers the upload process if valid.
    /// </summary>
    private async Task HandleSubmit()
    {
        if (_editContext.Validate() && _selectedFile != null)
        {
            await OnValidSubmit();
        }
    }

    /// <summary>
    /// Cancels the upload and closes the dialog.
    /// </summary>
    private async Task Cancel()
    {
        // Cancel ongoing upload if there is one
        await CancelUpload();
        MudDialog.Close(DialogResult.Cancel());
    }

    /// <summary>
    /// Attempts to cancel an ongoing upload via JavaScript interop.
    /// </summary>
    private async Task CancelUpload()
    {
        if (_isUploading && !string.IsNullOrEmpty(_currentUploadId))
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("artifactoUpload.cancelUpload", _currentUploadId);
                _isUploading = false;
                _currentUploadId = null;
                StateHasChanged();
            }
            catch
            {
                // Ignore errors when cancelling
            }
        }
    }

    /// <summary>
    /// Formats a file size value into a human-readable string (B, KB, MB, ...).
    /// </summary>
    /// <param name="bytes">Number of bytes.</param>
    /// <returns>Formatted file size string.</returns>
    private static string FormatFileSize(long bytes)
    {
        const int scale = 1024;
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

    /// <summary>
    /// Disposes of any managed resources used by the component.
    /// </summary>
    public void Dispose()
    {
        _dotNetRef?.Dispose();
    }
}
