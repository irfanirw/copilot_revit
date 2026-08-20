using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

public static class GeneratedCommand
{
    public static string Execute(UIApplication uiApp)
    {
        UIDocument uidoc = uiApp.ActiveUIDocument;
        Document doc = uidoc.Document;

        var selected = uidoc.Selection.GetElementIds();
        if (selected.Count == 0)
            return "No elements selected.";

        string names = string.Join("\n", selected
            .Select(id => doc.GetElement(id)?.Name ?? id.ToString()));

        return $"Selected elements:\n{names}";
    }
}
