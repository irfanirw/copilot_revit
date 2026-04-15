using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevCode.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ShowEditorCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiApp = commandData.Application;

            // Initialize the editor page with UIApplication on first use
            App.InitializeEditorPage(uiApp);

            // Toggle the dockable pane
            var pane = uiApp.GetDockablePane(App.EditorPaneId);
            if (pane != null)
            {
                if (pane.IsShown())
                    pane.Hide();
                else
                    pane.Show();
            }

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
