using RevAI.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace RevAI.UI;

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>
/// Custom control to render a chat message bubble.
/// </summary>
public class ChatBubbleControl : ContentControl
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        if (DataContext is ChatMessage message)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(8, 4, 8, 4),
                MaxWidth = 420,
            };

            Brush foreground;
            FontFamily? fontFamily = null;
            double fontSize = 13;
            FontStyle fontStyle = FontStyles.Normal;

            switch (message.Role)
            {
                case "user":
                    border.Background = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
                    border.HorizontalAlignment = HorizontalAlignment.Right;
                    foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));
                    break;

                case "assistant":
                    border.Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44));
                    border.HorizontalAlignment = HorizontalAlignment.Left;
                    foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4));
                    break;

                case "result":
                    border.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x2E));
                    border.HorizontalAlignment = HorizontalAlignment.Left;
                    foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
                    fontFamily = new FontFamily("Cascadia Code, Consolas, Courier New");
                    fontSize = 12;
                    break;

                case "error":
                    border.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x1E, 0x1E));
                    border.HorizontalAlignment = HorizontalAlignment.Left;
                    foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
                    fontFamily = new FontFamily("Cascadia Code, Consolas, Courier New");
                    fontSize = 12;
                    break;

                default: // system
                    border.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
                    border.HorizontalAlignment = HorizontalAlignment.Center;
                    foreground = new SolidColorBrush(Color.FromRgb(0xBA, 0xC2, 0xDE));
                    fontStyle = FontStyles.Italic;
                    break;
            }

            var paragraph = new Paragraph
            {
                FontSize = fontSize,
                Foreground = foreground,
                FontStyle = fontStyle,
                Margin = new Thickness(0),
            };
            if (fontFamily != null) paragraph.FontFamily = fontFamily;

            RenderFormattedText(paragraph, message.Content);

            var doc = new FlowDocument(paragraph)
            {
                PagePadding = new Thickness(0),
            };

            var richTextBox = new RichTextBox
            {
                Document = doc,
                IsReadOnly = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                IsTabStop = false,
                Cursor = Cursors.IBeam,
            };

            // Context menu: Copy selected text + Copy All
            var copyMenu = new ContextMenu();
            var copySelItem = new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
            copySelItem.Click += (s, args) =>
            {
                if (!string.IsNullOrEmpty(richTextBox.Selection.Text))
                    try { Clipboard.SetText(richTextBox.Selection.Text); } catch { }
            };
            var copyAllItem = new MenuItem { Header = "Copy All" };
            copyAllItem.Click += (s, args) =>
            {
                try { Clipboard.SetText(message.Content); } catch { }
            };
            copyMenu.Items.Add(copySelItem);
            copyMenu.Items.Add(copyAllItem);
            richTextBox.ContextMenu = copyMenu;

            border.Child = richTextBox;
            Content = border;
        }
    }

    private static void RenderFormattedText(Paragraph paragraph, string text)
    {
        paragraph.Inlines.Clear();

        // Split by **bold** markers and ```code``` blocks
        var parts = text.Split(["```"], StringSplitOptions.None);

        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1)
            {
                // Code block
                var codeText = parts[i];
                // Remove language identifier (e.g., "csharp\n")
                var newlineIdx = codeText.IndexOf('\n');
                if (newlineIdx > 0 && newlineIdx < 20)
                    codeText = codeText[(newlineIdx + 1)..];

                var run = new Run(codeText.Trim())
                {
                    FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF)),
                };
                paragraph.Inlines.Add(new LineBreak());
                paragraph.Inlines.Add(run);
                paragraph.Inlines.Add(new LineBreak());
            }
            else
            {
                // Regular text — handle **bold**
                RenderBoldText(paragraph, parts[i]);
            }
        }
    }

    private static void RenderBoldText(Paragraph paragraph, string text)
    {
        var segments = text.Split("**");
        for (int j = 0; j < segments.Length; j++)
        {
            if (string.IsNullOrEmpty(segments[j])) continue;

            var run = new Run(segments[j]);
            if (j % 2 == 1)
            {
                run.FontWeight = FontWeights.Bold;
            }
            paragraph.Inlines.Add(run);
        }
    }
}
