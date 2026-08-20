using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

public static class GeneratedCommand
{
    public static string Execute(UIApplication uiApp)
    {
        Document doc = uiApp.ActiveUIDocument.Document;
        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .OrderBy(v => v.Name)
            .Take(20)
            .Select(v => v.Name);

        return "Sample views:\n" + string.Join("\n", views);
    }
}
