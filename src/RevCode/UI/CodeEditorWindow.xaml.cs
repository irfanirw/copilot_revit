using Autodesk.Revit.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RevCode.UI;

public partial class CodeEditorWindow : Window
{
    private readonly UIApplication _uiApp;
    private bool _isExecuting;

    private const string DefaultTemplate = @"using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

public static class GeneratedCommand
{
    public static string Execute(UIApplication uiApp)
    {
        Document doc = uiApp.ActiveUIDocument.Document;
        
        // Write your code here
        
        return ""Done."";
    }
}
";

    public CodeEditorWindow(UIApplication uiApp)
    {
        InitializeComponent();
        _uiApp = uiApp;
        CodeEditor.Text = DefaultTemplate;
        CodeEditor.SelectionChanged += CodeEditor_SelectionChanged;
        CodeEditor.Loaded += (s, e) => { UpdateLineNumbers(); SyncLineNumberScroll(); };
        CodeEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(CodeEditor_ScrollChanged));
    }

    private void CodeEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLineNumbers();
    }

    private void UpdateLineNumbers()
    {
        int lineCount = CodeEditor.LineCount;
        if (lineCount < 1) lineCount = 1;

        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            if (i > 1) sb.AppendLine();
            sb.Append(i);
        }
        LineNumbers.Text = sb.ToString();
    }

    private void CodeEditor_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        SyncLineNumberScroll();
    }

    private void SyncLineNumberScroll()
    {
        var scrollViewer = FindChildScrollViewer(CodeEditor);
        if (scrollViewer != null)
        {
            LineNumberScroller.ScrollToVerticalOffset(scrollViewer.VerticalOffset);
        }
    }

    private static ScrollViewer? FindChildScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;
            var result = FindChildScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    private void RunCode_Click(object sender, RoutedEventArgs e)
    {
        ExecuteCode();
    }

    private void ClearOutput_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Text = string.Empty;
        StatusText.Text = "Ready";
    }

    private void ClearCode_Click(object sender, RoutedEventArgs e)
    {
        CodeEditor.Text = DefaultTemplate;
        OutputBox.Text = string.Empty;
        StatusText.Text = "Ready";
    }

    private void PinWindow_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Content = Topmost ? "\U0001F4CC Unpin" : "\U0001F4CC Pin";
    }

    private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            ExecuteCode();
            e.Handled = true;
        }
    }

    private void CodeEditor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        int caretIndex = CodeEditor.CaretIndex;
        string text = CodeEditor.Text;

        int line = 1;
        int col = 1;
        for (int i = 0; i < caretIndex && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }

        LineColText.Text = $"Ln {line}, Col {col}";
    }

    private void ExecuteCode()
    {
        if (_isExecuting) return;

        string code = CodeEditor.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(code))
        {
            AppendOutput("No code to execute.");
            return;
        }

        _isExecuting = true;
        StatusText.Text = "Executing...";

        var handler = App.ExecutionHandler;
        var externalEvent = App.ExternalEvent;

        if (handler == null || externalEvent == null)
        {
            AppendOutput("Error: Execution handler not initialized.");
            _isExecuting = false;
            StatusText.Text = "Error";
            return;
        }

        handler.SetCode(code, (result, success) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (success)
                {
                    AppendOutput($"✅ {result}");
                    StatusText.Text = "Executed successfully";
                }
                else
                {
                    AppendOutput($"❌ {result}");
                    StatusText.Text = "Execution failed";
                }
                _isExecuting = false;
            });
        });

        externalEvent.Raise();
    }

    private void AppendOutput(string text)
    {
        if (!string.IsNullOrEmpty(OutputBox.Text))
            OutputBox.Text += "\n";

        OutputBox.Text += $"[{DateTime.Now:HH:mm:ss}] {text}";
        OutputBox.ScrollToEnd();
    }
}
