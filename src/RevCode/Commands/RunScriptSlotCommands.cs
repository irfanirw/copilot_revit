// Auto-generated slot commands for the Scripts Gallery pulldown ribbon buttons.
// Each command reads App.ScriptSlots[N] at runtime so no recompile is needed
// when scripts are added — just restart Revit to refresh the gallery.

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevCode.Commands;

// ── Slot helpers ──────────────────────────────────────────────────────────────

internal static class ScriptSlotRunner
{
    internal static Result Run(int slot, ExternalCommandData commandData, ref string message)
    {
        try
        {
            App.InitializeEditorPage(commandData.Application);
            App.RunScriptAtSlot(slot, commandData.Application, (result, ok) => { /* fire-and-forget */ });
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}

// ── Slot 00–11 ───────────────────────────────────────────────────────────────

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot00Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(0, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot01Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(1, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot02Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(2, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot03Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(3, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot04Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(4, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot05Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(5, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot06Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(6, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot07Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(7, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot08Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(8, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot09Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(9, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot10Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(10, c, ref m); }

[Transaction(TransactionMode.Manual)][Regeneration(RegenerationOption.Manual)]
public class RunScriptSlot11Command : IExternalCommand
{ public Result Execute(ExternalCommandData c, ref string m, ElementSet e) => ScriptSlotRunner.Run(11, c, ref m); }
