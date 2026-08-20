using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

public static class GeneratedCommand
{
    public static string Execute(UIApplication uiApp)
    {
        Document doc = uiApp.ActiveUIDocument.Document;

        var walls = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
            .Cast<Element>()
            .Take(20)
            .Select(e => e.Id.IntegerValue + " - " + e.Name);

        return "Sample wall IDs:\n" + string.Join("\n", walls);
    }
}
