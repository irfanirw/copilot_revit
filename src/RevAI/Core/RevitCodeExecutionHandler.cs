using Autodesk.Revit.UI;

namespace RevAI.Core;

/// <summary>
/// Handles execution of AI-generated code within the Revit API context.
/// This runs on Revit's main thread, allowing safe access to the Revit API.
/// </summary>
public class RevitCodeExecutionHandler : IExternalEventHandler
{
    private string? _codeToExecute;
    private Action<string, bool>? _resultCallback;
    private readonly object _lock = new();

    public void SetCode(string code, Action<string, bool> callback)
    {
        lock (_lock)
        {
            _codeToExecute = code;
            _resultCallback = callback;
        }
    }

    public void Execute(UIApplication app)
    {
        string? code;
        Action<string, bool>? callback;

        lock (_lock)
        {
            code = _codeToExecute;
            callback = _resultCallback;
            _codeToExecute = null;
            _resultCallback = null;
        }

        if (string.IsNullOrEmpty(code) || callback == null)
            return;

        try
        {
            var compiler = new Services.CodeCompiler();
            var result = compiler.CompileAndExecute(code, app);
            callback(result, true);
        }
        catch (Exception ex)
        {
            callback($"Error: {ex.Message}\n\n{ex.StackTrace}", false);
        }
    }

    public string GetName() => "RevAI Code Execution Handler";
}
