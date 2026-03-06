using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Dialogs;

/// <summary>
/// Dialog component that uploads an SBOM for an existing artifact version.
/// </summary>
public partial class UploadSbomDialog : ComponentBase, IDisposable
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public required string ProjectId { get; set; }

    [Parameter]
    public required string ArtifactVersion { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private IBrowserFile? _selectedFile;
    private bool _isUploading;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private double _uploadProgress;
    private string _uploadStatus = string.Empty;
    private long _bytesUploaded;
    private long _totalBytes;
    private DotNetObjectReference<UploadSbomDialog>? _dotNetRef;
    private string? _currentUploadId;

    protected override void OnInitialized()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
    }

    private void OnFileSelected(InputFileChangeEventArgs e)
    {
        _selectedFile = e.File;
        StateHasChanged();
    }

    private async Task HandleSubmit()
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

            bool isAvailable = await JSRuntime.InvokeAsync<bool>("eval", "typeof artifactoUpload !== 'undefined'");
            if (!isAvailable)
            {
                throw new InvalidOperationException("Upload module not loaded");
            }

            string uploadUrl = $"{Navigation.BaseUri}projects/{Uri.EscapeDataString(ProjectId)}/artifacts/{Uri.EscapeDataString(ArtifactVersion)}/sbom";

            _currentUploadId = await JSRuntime.InvokeAsync<string>(
                "artifactoUpload.uploadFile",
                "sbomFileInput",
                uploadUrl,
                _dotNetRef,
                nameof(OnUploadProgress),
                nameof(OnUploadComplete),
                nameof(OnUploadError),
                "PUT",
                true);
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

    [JSInvokable]
    public async Task OnUploadComplete(object response)
    {
        _uploadProgress = 100;
        _uploadStatus = "Upload complete!";
        _isUploading = false;
        _currentUploadId = null;
        StateHasChanged();

        await Task.Delay(500);
        MudDialog.Close(DialogResult.Ok(true));
    }

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

    private async Task Cancel()
    {
        await CancelUpload();
        MudDialog.Close(DialogResult.Cancel());
    }

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
            }
        }
    }

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

    public void Dispose()
    {
        _dotNetRef?.Dispose();
    }
}