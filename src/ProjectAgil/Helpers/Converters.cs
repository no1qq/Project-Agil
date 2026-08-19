using System.Windows.Data;
using System.Windows.Media;
using ProjectAgil.Models;

namespace ProjectAgil.Helpers;

public sealed class TweakStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TweakStatus status
            ? status switch
            {
                TweakStatus.Optimized => new SolidColorBrush(Color.FromRgb(0x3F, 0xD6, 0x8C)),
                TweakStatus.NotOptimized => new SolidColorBrush(Color.FromRgb(0xF5, 0xA5, 0x24)),
                TweakStatus.PendingRestart => new SolidColorBrush(Color.FromRgb(0x4F, 0xA8, 0xFF)),
                TweakStatus.NotConfirmed => new SolidColorBrush(Color.FromRgb(0xFF, 0x77, 0x3D)),
                TweakStatus.Failed => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
                TweakStatus.Unsupported => new SolidColorBrush(Color.FromRgb(0x8A, 0x93, 0xA6)),
                _ => new SolidColorBrush(Color.FromRgb(0x8A, 0x93, 0xA6)),
            }
            : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class TweakStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is TweakStatus status
            ? status switch
            {
                TweakStatus.Optimized => "Applied",
                TweakStatus.NotOptimized => "Pending",
                TweakStatus.PendingRestart => "Needs restart",
                TweakStatus.NotConfirmed => "Did not stick",
                TweakStatus.Failed => "Failed",
                TweakStatus.Unsupported => "Not available",
                _ => "Unknown",
            }
            : "Unknown";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class VerdictToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is BenchmarkVerdict verdict
            ? verdict switch
            {
                BenchmarkVerdict.Better => new SolidColorBrush(Color.FromRgb(0x3F, 0xD6, 0x8C)),
                BenchmarkVerdict.Worse => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
                BenchmarkVerdict.NoChange => new SolidColorBrush(Color.FromRgb(0xF5, 0xA5, 0x24)),
                _ => new SolidColorBrush(Color.FromRgb(0x8A, 0x93, 0xA6)),
            }
            : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class GradeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var grade = value is int i ? i : 0;

        return grade switch
        {
            >= 85 => new SolidColorBrush(Color.FromRgb(0x3F, 0xD6, 0x8C)),
            >= 70 => new SolidColorBrush(Color.FromRgb(0x6D, 0xD4, 0x00)),
            >= 50 => new SolidColorBrush(Color.FromRgb(0xF5, 0xA5, 0x24)),
            >= 25 => new SolidColorBrush(Color.FromRgb(0xFF, 0x77, 0x3D)),
            _ => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;

        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not bool b || !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not bool b || !b;
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var flag = count > 0;

        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is not null;

        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = !string.IsNullOrWhiteSpace(value as string);

        if (parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
