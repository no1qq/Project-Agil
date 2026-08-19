using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ProjectAgil.Helpers;

public static class DialogHelper
{
    public static async Task<bool> ConfirmAsync(
        this IContentDialogService service,
        string title,
        string content,
        string primaryButton,
        string closeButton = "Cancel"
    )
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButton,
            CloseButtonText = closeButton,
        };

        var result = await service.ShowAsync(dialog, CancellationToken.None).ConfigureAwait(true);

        return result == ContentDialogResult.Primary;
    }
}
