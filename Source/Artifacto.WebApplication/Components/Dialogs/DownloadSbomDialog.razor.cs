using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Dialogs;

public record DownloadSbomSelection(string Format, string SpecVersion);

/// <summary>
/// Dialog that collects the desired SBOM output format and CycloneDX version.
/// </summary>
public partial class DownloadSbomDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public required string ArtifactVersion { get; set; }

    private readonly DownloadSbomModel _model = new();
    private EditForm _form = default!;

    private sealed class DownloadSbomModel
    {
        [Required]
        public string Format { get; set; } = "json";

        [Required]
        public string SpecVersion { get; set; } = "1.7";
    }

    private async Task HandleSubmit()
    {
        if (_form?.EditContext?.Validate() != true)
        {
            return;
        }

        await OnValidSubmit();
    }

    private Task OnValidSubmit()
    {
        MudDialog.Close(DialogResult.Ok(new DownloadSbomSelection(_model.Format, _model.SpecVersion)));
        return Task.CompletedTask;
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}