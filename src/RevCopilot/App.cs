using Autodesk.Revit.UI;
using RevCopilot.UI;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace RevCopilot;

public class App : IExternalApplication
{
    internal static UIControlledApplication? UiControlledApp { get; private set; }

    // Unique ID for the dockable pane
    internal static readonly DockablePaneId CopilotPaneId =
        new(new Guid("B2C3D4E5-F6A7-4B89-0D1E-FA6078BBCCDD"));

    private static CopilotPage? _copilotPage;

    public Result OnStartup(UIControlledApplication application)
    {
        UiControlledApp = application;

        // Register the dockable pane (must be done during startup)
        _copilotPage = new CopilotPage();
        application.RegisterDockablePane(CopilotPaneId, "RevCopilot — M365 Copilot", _copilotPage);

        // Build ribbon UI
        CreateRibbonUI(application);

        return Result.Succeeded;
    }

    /// <summary>
    /// Called by ShowCopilotCommand on first use to inject UIApplication context.
    /// </summary>
    internal static void InitializeCopilotPage(UIApplication uiApp)
    {
        _copilotPage?.Initialize(uiApp);
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    private void CreateRibbonUI(UIControlledApplication application)
    {
        const string tabName = "Code & Automations";

        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch
        {
            // Tab already exists (created by RevAI or RevCode) — safe to continue
        }

        var panel = application.CreateRibbonPanel(tabName, "M365 Copilot");

        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        var buttonData = new PushButtonData(
            "RevCopilotChat",
            "Copilot\nChat",
            assemblyPath,
            "RevCopilot.Commands.ShowCopilotCommand")
        {
            ToolTip = "Open Microsoft 365 Copilot inside Revit",
            LongDescription =
                "Chat with Microsoft 365 Copilot or any of your M365 Copilot agents directly " +
                "inside Revit. Sign in with your Microsoft 365 account to get started.\n\n" +
                "Requires Microsoft 365 Copilot license and an Azure AD app registration.",
            LargeImage = LoadEmbeddedImage("RevCopilot.Resources.icon32.png"),
            Image = LoadEmbeddedImage("RevCopilot.Resources.icon16.png")
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
