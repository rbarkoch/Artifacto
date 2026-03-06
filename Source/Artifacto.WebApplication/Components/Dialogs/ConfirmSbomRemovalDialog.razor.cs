using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace Artifacto.WebApplication.Components.Dialogs;

public partial class ConfirmSbomRemovalDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    private void Confirm()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}