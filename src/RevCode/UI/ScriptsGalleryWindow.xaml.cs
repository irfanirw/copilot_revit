using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RevCode.UI;

/// <summary>
/// View-model for a single script entry in the gallery list.
/// </summary>
internal sealed class ScriptItem
{
    public string Name { get; init; } = string.Empty;
    public string Meta { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
}

public partial class ScriptsGalleryWindow : Window
{
    private readonly UIApplication _uiApp;
    private readonly ObservableCollection<ScriptItem> _allItems = new();
    private DispatcherTimer? _progressTimer;
    private double _progressValue;

    public ScriptsGalleryWindow(UIApplication uiApp)
    {
        _uiApp = uiApp;
        InitializeComponent();
        LoadScripts();
    }

    // ── Script discovery ──────────────────────────────────────────────────────

    private void LoadScripts()
    {
        _allItems.Clear();

        if (!Directory.Exists(App.ScriptsFolder))
        {
            Directory.CreateDirectory(App.ScriptsFolder);
        }

        var files = Directory.GetFiles(App.ScriptsFolder, "*.cs", SearchOption.TopDirectoryOnly)
                             .OrderByDescending(f => File.GetLastWriteTime(f))
                             .ToArray();

        foreach (var path in files)
        {
            var info = new FileInfo(path);
            _allItems.Add(new ScriptItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Meta = $"Modified {info.LastWriteTime:dd MMM yyyy HH:mm}  ·  {info.Length / 1024.0:F1} KB",
                FilePath = path
            });
        }

        ApplyFilter(SearchBox.Text);
        UpdateHeader();
    }

    private void ApplyFilter(string query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allItems
            : (IEnumerable<ScriptItem>)_allItems.Where(s =>
                s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        ScriptsList.ItemsSource = filtered.ToList();
        EmptyState.Visibility = _allItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateHeader()
    {
        ScriptCountText.Text = _allItems.Count == 1 ? "1 script" : $"{_allItems.Count} scripts";
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void NewScript_Click(object sender, RoutedEventArgs e)
    {
        App.RequestLoadScript(string.Empty);   // opens editor with blank template

        // Show the editor pane
        try
        {
            var pane = _uiApp.GetDockablePane(App.EditorPaneId);
            if (pane != null && !pane.IsShown())
                pane.Show();
        }
        catch { /* pane may not be ready yet */ }

        SetStatus("New script opened in editor.");
        Close();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(App.ScriptsFolder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{App.ScriptsFolder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open folder: {ex.Message}");
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadScripts();
        SetStatus("Gallery refreshed.");
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void RunScript_Click(object sender, RoutedEventArgs e)
    {
        var path = (sender as Button)?.Tag as string;
        if (path == null || !File.Exists(path))
        {
            SetStatus("Script file not found.");
            return;
        }

        var handler = App.ExecutionHandler;
        var externalEvent = App.ExternalEvent;
        if (handler == null || externalEvent == null)
        {
            SetStatus("Error: execution handler not initialized.");
            return;
        }

        string code;
        try { code = File.ReadAllText(path); }
        catch (Exception ex) { SetStatus($"Could not read file: {ex.Message}"); return; }

        SetStatus($"Running {Path.GetFileNameWithoutExtension(path)}…");
        StartProgress();

        handler.SetCode(code, (result, success) =>
        {
            Dispatcher.Invoke(() =>
            {
                StopProgress();
                SetStatus(success
                    ? $"✅ {Path.GetFileNameWithoutExtension(path)} completed."
                    : $"❌ {result}");
            });
        });

        externalEvent.Raise();
    }

    private void LoadScript_Click(object sender, RoutedEventArgs e)
    {
        var path = (sender as Button)?.Tag as string;
        if (path == null || !File.Exists(path))
        {
            SetStatus("Script file not found.");
            return;
        }

        string code;
        try { code = File.ReadAllText(path); }
        catch (Exception ex) { SetStatus($"Could not read file: {ex.Message}"); return; }

        // Push code to editor and show the pane
        App.RequestLoadScript(code);

        try
        {
            var pane = _uiApp.GetDockablePane(App.EditorPaneId);
            if (pane != null && !pane.IsShown())
                pane.Show();
        }
        catch { /* pane may not be ready */ }

        SetStatus($"Loaded {Path.GetFileNameWithoutExtension(path)} in editor.");
        Close();
    }

    private void DeleteScript_Click(object sender, RoutedEventArgs e)
    {
        var path = (sender as Button)?.Tag as string;
        if (path == null) return;

        var name = Path.GetFileNameWithoutExtension(path);
        var result = MessageBox.Show(
            $"Delete \"{name}\" from the gallery?\nThis cannot be undone.",
            "Delete Script",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            File.Delete(path);
            LoadScripts();
            SetStatus($"Deleted {name}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Could not delete: {ex.Message}");
        }
    }

    // ── Progress / status ─────────────────────────────────────────────────────

    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }

    private void StartProgress()
    {
        _progressValue = 0;
        RunProgress.Value = 0;
        RunProgress.Visibility = Visibility.Visible;

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _progressTimer.Tick += (s, e) =>
        {
            double inc = _progressValue < 30 ? 3.0 : 0.4;
            _progressValue = Math.Min(_progressValue + inc, 85);
            RunProgress.Value = _progressValue;
        };
        _progressTimer.Start();
    }

    private void StopProgress()
    {
        _progressTimer?.Stop();
        _progressTimer = null;
        RunProgress.Value = 100;

        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        t.Tick += (s, e) => { t.Stop(); RunProgress.Visibility = Visibility.Collapsed; };
        t.Start();
    }
}
