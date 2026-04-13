using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace RevAI.Services;

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
        // Wrap code with usings if not already present
        string fullCode = WrapCode(code);

        // Parse the syntax tree
        var syntaxTree = CSharpSyntaxTree.ParseText(fullCode);

        // Gather references
        var references = GetMetadataReferences();

        // Compile
        var compilation = CSharpCompilation.Create(
            $"MKH_Dynamic_{Guid.NewGuid():N}",
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

        // Load into a collectible AssemblyLoadContext for proper cleanup
        var loadContext = new CollectibleLoadContext();
        var assembly = loadContext.LoadFromStream(ms);

        try
        {
            // Find and invoke GeneratedCommand.Execute(UIApplication)
            var type = assembly.GetType("GeneratedCommand")
                ?? throw new InvalidOperationException(
                    "Generated code must contain a class named 'GeneratedCommand'.");

            var method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException(
                    "GeneratedCommand must have a static method 'Execute(UIApplication)'.");

            try
            {
                var result = method.Invoke(null, [uiApp]);
                return result?.ToString() ?? "Command executed successfully (no output).";
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new InvalidOperationException(
                    $"Runtime error in generated code: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static string WrapCode(string code)
    {
        // Check if the code already has using statements
        bool hasUsings = code.TrimStart().StartsWith("using ", StringComparison.Ordinal);

        if (hasUsings)
            return code;

        // Prepend default usings
        return string.Join("\n", DefaultUsings) + "\n\n" + code;
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        // Add runtime assemblies
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
                    // Skip assemblies that can't be loaded
                }
            }
        }

        // Add Revit API assemblies
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

/// <summary>
/// A collectible assembly load context that allows unloading dynamically compiled assemblies.
/// </summary>
internal class CollectibleLoadContext : AssemblyLoadContext
{
    public CollectibleLoadContext() : base(isCollectible: true) { }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Return null to fall back to the default load context
        return null;
    }
}
