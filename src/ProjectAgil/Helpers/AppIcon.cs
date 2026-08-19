using System.Windows.Media.Imaging;
using Wpf.Ui.Appearance;

namespace ProjectAgil.Helpers;

internal static class AppIcon
{
    private const string ResourceKey = "AppIconImage";
    private const string LightArtwork = "pack://application:,,,/Assets/app-light.ico";
    private const string DarkArtwork = "pack://application:,,,/Assets/app-dark.ico";

    private static readonly BitmapImage ForDarkTheme = Load(LightArtwork);
    private static readonly BitmapImage ForLightTheme = Load(DarkArtwork);

    public static void FollowTheme()
    {
        Apply(ApplicationThemeManager.GetAppTheme());
        ApplicationThemeManager.Changed += (theme, _) => Apply(theme);
    }

    private static void Apply(ApplicationTheme theme) =>
        Application.Current.Resources[ResourceKey] =
            theme == ApplicationTheme.Light ? ForLightTheme : ForDarkTheme;

    private static BitmapImage Load(string path)
    {
        var image = new BitmapImage();

        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();

        return image;
    }
}
