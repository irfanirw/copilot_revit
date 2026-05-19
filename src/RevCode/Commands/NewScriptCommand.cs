using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevCode.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class NewScriptCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiApp = commandData.Application;
            App.InitializeEditorPage(uiApp);

            // Load blank template into editor
            App.RequestLoadScript(string.Empty);

            // Show the editor pane
            var pane = uiApp.GetDockablePane(App.EditorPaneId);
            if (pane != null && !pane.IsShown())
                pane.Show();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
