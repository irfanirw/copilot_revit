using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace RevCode.Services;

public class CodeCompiler
{
    private static readonly string[] DefaultUsings =
    [
        "using Autodesk.Revit.DB;",
        "using Autodesk.Revit.DB.Architecture;",
        "using Autodesk.Revit.DB.Structure;",
        "using Autodesk.Revit.UI;",
        "using Autodesk.Revit.UI.Selection;",
        "using System;",
        "using System.Linq;",
        "using System.Collections.Generic;",
        "using System.Text;",
    ];

    public string CompileAndExecute(string code, UIApplication uiApp)
    {
        string fullCode = WrapCode(code);

        var syntaxTree = CSharpSyntaxTree.ParseText(fullCode);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            $"RevCode_Dynamic_{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.AnyCpu));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString());
            throw new InvalidOperationException(
                $"Compilation failed:\n{string.Join("\n", errors)}");
        }

        ms.Seek(0, SeekOrigin.Begin);

        var loadContext = new CollectibleLoadContext();
        var assembly = loadContext.LoadFromStream(ms);

        try
        {
            // Strategy 1: Look for GeneratedCommand.Execute(UIApplication) — static method
            var genType = assembly.GetType("GeneratedCommand");
            if (genType != null)
            {
                var method = genType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    return InvokeMethod(method, null, [uiApp]);
                }
            }

            // Strategy 2: Look for any class with a public static Execute(UIApplication) method
            foreach (var type in assembly.GetExportedTypes())
            {
                var method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static, null,
                    [typeof(UIApplication)], null);
                if (method != null)
                {
                    return InvokeMethod(method, null, [uiApp]);
                }
            }

            // Strategy 3: Look for any class with a public static Run(UIApplication) method
            foreach (var type in assembly.GetExportedTypes())
            {
                var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, null,
                    [typeof(UIApplication)], null);
                if (method != null)
                {
                    return InvokeMethod(method, null, [uiApp]);
                }
            }

            // Check if user wrote IExternalCommand pattern and give helpful error
            var cmdType = assembly.GetExportedTypes()
                .FirstOrDefault(t => typeof(Autodesk.Revit.UI.IExternalCommand).IsAssignableFrom(t) && !t.IsAbstract);
            if (cmdType != null)
            {
                throw new InvalidOperationException(
                    $"Found IExternalCommand class '{cmdType.Name}', but RevCode cannot create ExternalCommandData.\n\n" +
                    "Please refactor your code to use this pattern instead:\n\n" +
                    "public static class GeneratedCommand\n" +
                    "{\n" +
                    "    public static string Execute(UIApplication uiApp)\n" +
                    "    {\n" +
                    "        Document doc = uiApp.ActiveUIDocument.Document;\n" +
                    "        // your code here\n" +
                    "        return \"result\";\n" +
                    "    }\n" +
                    "}");
            }

            throw new InvalidOperationException(
                "No executable entry point found. Code must contain a class with:\n" +
                "  • public static string Execute(UIApplication uiApp)\n\n" +
                "Example:\n" +
                "public static class GeneratedCommand\n" +
                "{\n" +
                "    public static string Execute(UIApplication uiApp) { ... }\n" +
                "}");
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static string InvokeMethod(MethodInfo method, object? target, object[] args)
    {
        try
        {
            var result = method.Invoke(target, args);
            return result?.ToString() ?? "Command executed successfully (no output).";
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException(
                $"Runtime error: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
        }
    }

    private static string WrapCode(string code)
    {
        bool hasUsings = code.TrimStart().StartsWith("using ", StringComparison.Ordinal);

        if (hasUsings)
            return code;

        return string.Join("\n", DefaultUsings) + "\n\n" + code;
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator) ?? [];

        foreach (var assemblyPath in trustedAssemblies)
        {
            if (File.Exists(assemblyPath))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assemblyPath));
                }
                catch
                {
                }
            }
        }

        AddRevitAssemblyReference(references, typeof(Autodesk.Revit.DB.Document).Assembly);
        AddRevitAssemblyReference(references, typeof(Autodesk.Revit.UI.UIApplication).Assembly);

        return references;
    }

    private static void AddRevitAssemblyReference(List<MetadataReference> references, Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
        {
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
    }
}

internal class CollectibleLoadContext : AssemblyLoadContext
{
    public CollectibleLoadContext() : base(isCollectible: true) { }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        return null;
    }
}
