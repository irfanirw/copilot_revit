using Autodesk.Revit.UI;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RevCode.UI;

public partial class CodeEditorPage : Page, IDockablePaneProvider
{
    private UIApplication? _uiApp;
    private bool _isExecuting;
    private bool _isInitialized;

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

    public CodeEditorPage()
    {
        InitializeComponent();

        SetupEditor();
        CodeEditor.Text = DefaultTemplate;
        CodeEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        data.FrameworkElement = this;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Tabbed,
            TabBehind = Autodesk.Revit.UI.DockablePanes.BuiltInDockablePanes.PropertiesPalette
        };
    }

    public void Initialize(UIApplication uiApp)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _uiApp = uiApp;

        // Bind F5 to run
        var runCommand = new RoutedCommand();
        runCommand.InputGestures.Add(new KeyGesture(Key.F5));
        CommandBindings.Add(new CommandBinding(runCommand, (s, e) => ExecuteCode()));
    }

    private void SetupEditor()
    {
        CodeEditor.Options.EnableHyperlinks = false;
        CodeEditor.Options.EnableEmailHyperlinks = false;
        CodeEditor.Options.ConvertTabsToSpaces = true;
        CodeEditor.Options.IndentationSize = 4;

        CodeEditor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70));

        CodeEditor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x45, 0x47, 0x5A));
        CodeEditor.TextArea.SelectionForeground = null;

        ApplyDarkThemeHighlighting();
    }

    private void ApplyDarkThemeHighlighting()
    {
        var highlighting = HighlightingManager.Instance.GetDefinition("C#");
        if (highlighting == null) return;

        foreach (var color in highlighting.NamedHighlightingColors)
        {
            switch (color.Name)
            {
                case "Comment":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x6C, 0x70, 0x86));
                    break;
                case "String":
                case "Char":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
                    break;
                case "Preprocessor":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x94, 0xE2, 0xD5));
                    break;
                case "NumberLiteral":
                case "TrueFalse":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xFA, 0xB3, 0x87));
                    break;
                case "Keywords":
                case "GotoKeywords":
                case "ContextKeywords":
                case "NamespaceKeywords":
                case "SemanticKeywords":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xCB, 0xA6, 0xF7));
                    break;
                case "ValueTypeKeywords":
                case "ReferenceTypeKeywords":
                case "TypeKeywords":
                case "Modifiers":
                case "Visibility":
                case "ParameterModifiers":
                case "NullOrValueKeywords":
                case "CheckedKeyword":
                case "GetSetAddRemove":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
                    break;
                case "ExceptionKeywords":
                case "UnsafeKeywords":
                case "ThisOrBaseReference":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
                    break;
                case "OperatorKeywords":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x89, 0xDC, 0xFE));
                    break;
                case "Punctuation":
                    color.Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xBA, 0xC2, 0xDE));
                    break;
            }
        }

        CodeEditor.SyntaxHighlighting = highlighting;
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        LineColText.Text = $"Ln {CodeEditor.TextArea.Caret.Line}, Col {CodeEditor.TextArea.Caret.Column}";
    }

    private void RunCode_Click(object sender, RoutedEventArgs e)
    {
        ExecuteCode();
    }

    private void PasteCode_Click(object sender, RoutedEventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            CodeEditor.Text = Clipboard.GetText();
        }
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
        // No-op: dockable panes are managed by Revit
    }

    private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            ExecuteCode();
            e.Handled = true;
        }
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
