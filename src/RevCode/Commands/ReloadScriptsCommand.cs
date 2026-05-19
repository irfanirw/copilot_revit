using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevCode.Commands;

[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public class ReloadScriptsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            int count = App.ReloadScripts();

            TaskDialog.Show("RevCode — Scripts Gallery",
                count == 0
                    ? "No scripts found in the scripts folder.\n\nUse '⭐ Gallery' in the code editor to save a script."
                    : $"{count} script{(count == 1 ? "" : "s")} loaded.");

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
