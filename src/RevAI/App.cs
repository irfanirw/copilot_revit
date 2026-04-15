using Autodesk.Revit.UI;
using RevAI.Core;
using RevAI.UI;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace RevAI;

public class App : IExternalApplication
{
    internal static ExternalEvent? ExternalEvent { get; private set; }
    internal static RevitCodeExecutionHandler? ExecutionHandler { get; private set; }
    internal static UIControlledApplication? UiControlledApp { get; private set; }

    internal static readonly DockablePaneId ChatPaneId = new(new Guid("A1B2C3D4-E5F6-4A89-9D1E-FA6078AABBCC"));
    private static ChatPage? _chatPage;

    public Result OnStartup(UIControlledApplication application)
    {
        UiControlledApp = application;

        // Register the external event handler
        ExecutionHandler = new RevitCodeExecutionHandler();
        ExternalEvent = ExternalEvent.Create(ExecutionHandler);

        // Register dockable pane
        _chatPage = new ChatPage();
        application.RegisterDockablePane(ChatPaneId, "RevAI - AI Assistant", _chatPage);

        // Create the ribbon tab and panel
        CreateRibbonUI(application);

        return Result.Succeeded;
    }

    internal static void InitializeChatPage(UIApplication uiApp)
    {
        _chatPage?.Initialize(uiApp);
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
            // Tab may already exist
        }

        var panel = application.CreateRibbonPanel(tabName, "AI Assistant");

        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        var buttonData = new PushButtonData(
            "RevAIChat",
            "RevAI",
            assemblyPath,
            "RevAI.Commands.ShowChatCommand")
        {
            ToolTip = "Open AI Chat to interact with Revit using natural language",
            LongDescription = "Type natural language commands to create, modify, query, and automate tasks in Revit using AI-powered code generation.",
            LargeImage = LoadEmbeddedImage("RevAI.Resources.icon32.png"),
            Image = LoadEmbeddedImage("RevAI.Resources.icon16.png")
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
