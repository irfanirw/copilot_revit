using Autodesk.Revit.UI;
using RevCode.Core;
using RevCode.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;



namespace RevCode;

public class App : IExternalApplication
{
    internal static ExternalEvent? ExternalEvent { get; private set; }
    internal static CodeExecutionHandler? ExecutionHandler { get; private set; }

    /// <summary>Folder where gallery scripts (.cs files) are stored.</summary>
    internal static string ScriptsFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "RevCode", "Scripts");

    /// <summary>Raised when the gallery requests a script to be loaded into the editor.</summary>
    internal static event Action<string>? ScriptLoadRequested;

    /// <summary>Load script content into the code editor pane.</summary>
    internal static void RequestLoadScript(string code) => ScriptLoadRequested?.Invoke(code);

    // Register before any Roslyn types are resolved so that the CLR does not
    // load our bundled copies when Dynamo may already have loaded its own.
    static App()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveRoslynAssembly;
    }

    private static Assembly? ResolveRoslynAssembly(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        if (name == null ||
            (!name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) &&
             !name.Equals("ICSharpCode.AvalonEdit", StringComparison.OrdinalIgnoreCase)))
            return null;

        // Prefer whatever version is already loaded in the process (e.g. Dynamo's copy)
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a =>
            {
                try { return string.Equals(new AssemblyName(a.FullName!).Name, name, StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });
        if (loaded != null)
            return loaded;

        // Fall back to our bundled copy in the libs/ sub-folder
        var dir = Path.GetDirectoryName(typeof(App).Assembly.Location)!;
        var libPath = Path.Combine(dir, "libs", name + ".dll");
        return File.Exists(libPath) ? Assembly.LoadFrom(libPath) : null;
    }

    internal static readonly DockablePaneId EditorPaneId = new(new Guid("B7D4E2A1-C3F5-4A89-9D1E-FA6078BBCCDD"));
    private static LazyEditorPaneProvider? _lazyProvider;

    public Result OnStartup(UIControlledApplication application)
    {
        ExecutionHandler = new CodeExecutionHandler();
        ExternalEvent = ExternalEvent.Create(ExecutionHandler);

        // Ensure scripts folder exists
        Directory.CreateDirectory(ScriptsFolder);

        // Register a lightweight provider — CodeEditorPage (which loads AvalonEdit)
        // is created lazily the first time the pane is shown, not at Revit startup.
        // This ensures AvalonEdit is never loaded into the Default AssemblyLoadContext
        // before Dynamo has a chance to load its own copy.
        _lazyProvider = new LazyEditorPaneProvider();
        application.RegisterDockablePane(EditorPaneId, "RevCode - C# Editor", _lazyProvider);

        CreateRibbonUI(application);

        return Result.Succeeded;
    }

    internal static void InitializeEditorPage(UIApplication uiApp)
    {
        _lazyProvider?.SetUiApp(uiApp);
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        ExternalEvent?.Dispose();
        return Result.Succeeded;
    }

    private void CreateRibbonUI(UIControlledApplication application)
    {
        string tabName = "Code & Automations";

        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch
        {
            // Tab may already exist (shared with RevAI)
        }

        var panel = application.CreateRibbonPanel(tabName, "Code Editor");

        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        var buttonData = new PushButtonData(
            "RevCodeEditor",
            "RevCode",
            assemblyPath,
            "RevCode.Commands.ShowEditorCommand")
        {
            ToolTip = "Open C# code editor for Revit API automation",
            LongDescription = "Write and execute C# code directly against the Revit API. Ideal for quick scripts, testing API calls, and automation tasks.",
            LargeImage = LoadEmbeddedImage("RevCode.Resources.icon32.png"),
            Image = LoadEmbeddedImage("RevCode.Resources.icon16.png")
        };

        panel.AddItem(buttonData);

        // ---- Scripts Gallery panel ----
        CreateScriptsGalleryPanel(application, tabName, assemblyPath);
    }

    // ── Scripts Gallery panel ─────────────────────────────────────────────────

    /// <summary>Maps ComboBoxMember.Name → file path (since Revit's ComboBoxMember has no AssociatedData).</summary>
    private static readonly Dictionary<string, string> _comboMemberPaths = new(StringComparer.Ordinal);

    /// <summary>The ribbon ComboBox used as the scripts dropdown. Kept for runtime reload.</summary>
    internal static ComboBox? ScriptsComboBox { get; private set; }

    private void CreateScriptsGalleryPanel(UIControlledApplication application, string tabName, string assemblyPath)
    {
        var galleryPanel = application.CreateRibbonPanel(tabName, "RevCode Scripts Gallery");

        // ── ComboBox: dropdown list of saved scripts ──
        var combo = (ComboBox)galleryPanel.AddItem(new ComboBoxData("RevCodeScriptsCombo"));
        combo.ToolTip = "Select a saved script to load its code into the editor";
        ScriptsComboBox = combo;
        PopulateScriptsCombo(combo);
        combo.CurrentChanged += OnScriptSelected;

        // ── Three stacked action buttons ──
        galleryPanel.AddStackedItems(
            new PushButtonData("RevCodeGalleryNew", "New Script", assemblyPath,
                               "RevCode.Commands.NewScriptCommand")
            {
                ToolTip = "Open the code editor with a blank script",
                Image = LoadEmbeddedImage("RevCode.Resources.icon16.png"),
            },
            new PushButtonData("RevCodeGalleryReload", "Reload Scripts", assemblyPath,
                               "RevCode.Commands.ReloadScriptsCommand")
            {
                ToolTip = "Refresh the dropdown with the latest scripts from the scripts folder",
                Image = LoadEmbeddedImage("RevCode.Resources.icon16.png"),
            },
            new PushButtonData("RevCodeGalleryManage", "Manage Gallery", assemblyPath,
                               "RevCode.Commands.ShowScriptsGalleryCommand")
            {
                ToolTip = "Open Scripts Gallery to run, load, or delete saved scripts",
                Image = LoadEmbeddedImage("RevCode.Resources.icon16.png"),
            });
    }

    /// <summary>Populates (or refreshes) the ComboBox from the scripts folder.</summary>
    internal static void PopulateScriptsCombo(ComboBox combo)
    {
        var scripts = Directory.Exists(ScriptsFolder)
            ? Directory.GetFiles(ScriptsFolder, "*.cs", SearchOption.TopDirectoryOnly)
                       .OrderByDescending(f => File.GetLastWriteTime(f))
                       .ToArray()
            : Array.Empty<string>();

        // Update slot map
        for (int i = 0; i < ScriptSlots.Length; i++)
            ScriptSlots[i] = i < scripts.Length ? scripts[i] : null;

        // Build set of names already in the combo so we skip duplicates
        var existingNames = new HashSet<string>(
            combo.GetItems().Select(m => m.Name), StringComparer.Ordinal);

        // Refresh ItemText for existing members; mark deleted ones
        foreach (var member in combo.GetItems())
        {
            if (_comboMemberPaths.TryGetValue(member.Name, out var p))
            {
                member.ItemText = File.Exists(p)
                    ? Path.GetFileNameWithoutExtension(p)
                    : $"(removed) {member.ItemText}";
            }
        }

        // Add newly discovered scripts that don't yet have a member
        var trackedPaths = new HashSet<string>(_comboMemberPaths.Values, StringComparer.OrdinalIgnoreCase);
        foreach (var path in scripts.Where(p => !trackedPaths.Contains(p)))
        {
            var baseName = Path.GetFileNameWithoutExtension(path);
            var memberName = $"RCS_{baseName}";

            // Ensure unique member name (append counter if collides)
            int suffix = 0;
            while (existingNames.Contains(memberName))
                memberName = $"RCS_{baseName}_{++suffix}";

            try
            {
                combo.AddItem(new ComboBoxMemberData(memberName, baseName));
                _comboMemberPaths[memberName] = path;
                existingNames.Add(memberName);
            }
            catch { /* ignore, already added */ }
        }
    }

    /// <summary>Reloads the scripts ComboBox from the scripts folder (called by ReloadScriptsCommand).</summary>
    internal static int ReloadScripts()
    {
        if (ScriptsComboBox is not null)
            PopulateScriptsCombo(ScriptsComboBox);

        // Count live scripts (paths that still exist on disk)
        return _comboMemberPaths.Values.Count(File.Exists);
    }

    /// <summary>Fired when the user selects an item in the scripts ComboBox.</summary>
    private static void OnScriptSelected(object? sender, EventArgs e)
    {
        if (sender is not ComboBox combo) return;
        var memberName = combo.Current?.Name;
        if (memberName is null) return;
        if (!_comboMemberPaths.TryGetValue(memberName, out var path) || !File.Exists(path)) return;

        try
        {
            var code = File.ReadAllText(path);
            RequestLoadScript(code);
        }
        catch { /* silently ignore read errors */ }
    }


    /// <summary>Stores script file paths assigned to pulldown button slots at startup.</summary>
    internal static readonly string?[] ScriptSlots = new string?[12];

    /// <summary>Executes the script at the given slot index (called by RunScriptSlotNN commands).</summary>
    internal static void RunScriptAtSlot(int slot, UIApplication uiApp, Action<string, bool> callback)
    {
        var path = slot < ScriptSlots.Length ? ScriptSlots[slot] : null;
        if (path == null || !File.Exists(path))
        {
            callback("Script file not found. Reopen Revit to refresh the gallery.", false);
            return;
        }

        InitializeEditorPage(uiApp);
        var code = File.ReadAllText(path);

        var handler = ExecutionHandler;
        var externalEvent = ExternalEvent;
        if (handler == null || externalEvent == null) { callback("Execution handler not initialized.", false); return; }

        handler.SetCode(code, callback);
        externalEvent.Raise();
    }

    private static BitmapImage? LoadEmbeddedImage(string resourceName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Lightweight dockable-pane provider that defers creation of CodeEditorPage
/// (and therefore the loading of ICSharpCode.AvalonEdit.dll) until the pane
/// is first shown. This ensures AvalonEdit is never loaded into the Default
/// AssemblyLoadContext before Dynamo has a chance to load its own copy.
/// </summary>
internal class LazyEditorPaneProvider : IDockablePaneProvider
{
    private UIApplication? _pendingUiApp;
    private RevCode.UI.CodeEditorPage? _page;

    /// <summary>Called by ShowEditorCommand to pass the live UIApplication.</summary>
    internal void SetUiApp(UIApplication uiApp)
    {
        _pendingUiApp = uiApp;
        _page?.Initialize(uiApp);   // no-op if already initialized
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        // Revit calls this the first time the pane is displayed.
        // At this point AvalonEdit can be loaded safely (Dynamo has already
        // had its chance to register its copy via PreloadDynamoCoreDlls).
        if (_page == null)
        {
            _page = new RevCode.UI.CodeEditorPage();
            if (_pendingUiApp != null)
                _page.Initialize(_pendingUiApp);
        }

        _page.SetupDockablePane(data);
    }
}
