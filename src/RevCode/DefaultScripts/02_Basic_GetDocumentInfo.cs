using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

public static class GeneratedCommand
{
    public static string Execute(UIApplication uiApp)
    {
        Document doc = uiApp.ActiveUIDocument.Document;

        return $"Document: {doc.Title}\nPath: {doc.PathName}\nProject Number: {doc.ProjectInformation?.Number ?? "N/A"}";
    }
}
