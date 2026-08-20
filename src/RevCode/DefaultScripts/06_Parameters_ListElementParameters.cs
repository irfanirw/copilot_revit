using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

public static class GeneratedCommand
{
    public static string Execute(UIApplication uiApp)
    {
        Document doc = uiApp.ActiveUIDocument.Document;
        var selected = uiApp.ActiveUIDocument.Selection.GetElementIds();

        if (selected.Count == 0)
            return "Select an element to inspect its parameters.";

        var first = doc.GetElement(selected[0]);
        if (first == null)
            return "Selected element not found.";

        var parameters = first.Parameters
            .Cast<Parameter>()
            .Where(p => p.Definition != null)
            .OrderBy(p => p.Definition.Name)
            .Take(20)
            .Select(p => $"{p.Definition.Name} = {p.AsValueString() ?? p.AsString() ?? "<empty>"}");

        return "Parameters:\n" + string.Join("\n", parameters);
    }
}
