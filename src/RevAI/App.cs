using Autodesk.Revit.UI;
using RevAI.Core;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace RevAI;

public class App : IExternalApplication
{
    internal static ExternalEvent? ExternalEvent { get; private set; }
    internal static RevitCodeExecutionHandler? ExecutionHandler { get; private set; }
    internal static UIControlledApplication? UiControlledApp { get; private set; }

    public Result OnStartup(UIControlledApplication application)
    {
        UiControlledApp = application;

        // Register the external event handler
        ExecutionHandler = new RevitCodeExecutionHandler();
        ExternalEvent = ExternalEvent.Create(ExecutionHandler);

        // Create the ribbon tab and panel
        CreateRibbonUI(application);

        return Result.Succeeded;
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
