using Autodesk.Revit.UI;

namespace RevCode.Core;

public class CodeExecutionHandler : IExternalEventHandler
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
            callback($"Error: {ex.Message}", false);
        }
    }

    public string GetName() => "RevCode Execution Handler";
}
