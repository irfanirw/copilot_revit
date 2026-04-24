using Autodesk.Revit.UI;
using RevCode.Core;
using RevCode.UI;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace RevCode;

public class App : IExternalApplication
{
    internal static ExternalEvent? ExternalEvent { get; private set; }
    internal static CodeExecutionHandler? ExecutionHandler { get; private set; }

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
