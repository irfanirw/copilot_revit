using Autodesk.Revit.UI;
using RevCode.Core;
using RevCode.UI;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace RevCode;

public class App : IExternalApplication
{
    internal static ExternalEvent? ExternalEvent { get; private set; }
    internal static CodeExecutionHandler? ExecutionHandler { get; private set; }

    internal static readonly DockablePaneId EditorPaneId = new(new Guid("B7D4E2A1-C3F5-4A89-9D1E-FA6078BBCCDD"));
    private static CodeEditorPage? _editorPage;

    public Result OnStartup(UIControlledApplication application)
    {
        ExecutionHandler = new CodeExecutionHandler();
        ExternalEvent = ExternalEvent.Create(ExecutionHandler);

        // Register dockable pane
        _editorPage = new CodeEditorPage();
        application.RegisterDockablePane(EditorPaneId, "RevCode - C# Editor", _editorPage);

        CreateRibbonUI(application);

        return Result.Succeeded;
    }

    internal static void InitializeEditorPage(UIApplication uiApp)
    {
        _editorPage?.Initialize(uiApp);
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
