using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

public static class GeneratedCommand
{
    public static string Execute(UIApplication uiApp)
    {
        Document doc = uiApp.ActiveUIDocument.Document;
        var warnings = doc.GetWarnings();

        if (warnings.Count == 0)
            return "No warnings in the document.";

        var lines = warnings.Cast<Warning>()
            .Take(20)
            .Select(w => w.GetDescriptionText());

        return "Warnings:\n" + string.Join("\n", lines);
    }
}
