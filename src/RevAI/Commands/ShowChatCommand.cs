using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevAI.UI;

namespace RevAI.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ShowChatCommand : IExternalCommand
{
    private static ChatWindow? _chatWindow;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (_chatWindow == null || !_chatWindow.IsLoaded)
            {
                _chatWindow = new ChatWindow(commandData.Application);
                _chatWindow.Show();
            }
            else
            {
                _chatWindow.Activate();
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
