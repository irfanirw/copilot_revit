using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevCode.UI;

namespace RevCode.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ShowEditorCommand : IExternalCommand
{
    private static CodeEditorWindow? _editorWindow;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (_editorWindow == null || !_editorWindow.IsLoaded)
            {
                _editorWindow = new CodeEditorWindow(commandData.Application);
                _editorWindow.Show();
            }
            else
            {
                _editorWindow.Activate();
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
