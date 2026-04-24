using RevCopilot.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RevCopilot.UI;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is false;
}

/// <summary>Returns the bubble background brush based on message role.</summary>
[ValueConversion(typeof(MessageRole), typeof(Brush))]
public class RoleToBubbleBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush UserBrush =
        new(Color.FromRgb(0x7B, 0x61, 0xFF));   // M365 Copilot purple

    private static readonly SolidColorBrush AssistantBrush =
        new(Color.FromRgb(0x25, 0x25, 0x40));   // Dark blue-purple

    private static readonly SolidColorBrush SystemBrush =
        new(Color.FromRgb(0x3A, 0x3A, 0x5C));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is MessageRole role
            ? role == MessageRole.User ? UserBrush
            : role == MessageRole.System ? SystemBrush
            : AssistantBrush
            : AssistantBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}

/// <summary>Returns HorizontalAlignment based on message role.</summary>
[ValueConversion(typeof(MessageRole), typeof(HorizontalAlignment))]
public class RoleToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is MessageRole.User ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}

/// <summary>Returns the sender label text from a MessageRole.</summary>
[ValueConversion(typeof(MessageRole), typeof(string))]
public class RoleToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is MessageRole role
            ? role switch
            {
                MessageRole.User => "You",
                MessageRole.Assistant => "Copilot",
                _ => ""
            }
            : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}

/// <summary>Hides the sender label for System messages.</summary>
[ValueConversion(typeof(MessageRole), typeof(Visibility))]
public class RoleToLabelVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is MessageRole.System ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
