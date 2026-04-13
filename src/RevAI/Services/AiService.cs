using Azure.Identity;
using RevAI.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevAI.Services;

public class AiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly List<AiMessage> _conversationHistory = [];

    private AiProvider _provider = AiProvider.Copilot;

    // Copilot (Azure OpenAI) settings
    private string _endpoint = string.Empty;
    private string _deploymentName = string.Empty;
    private string _tenantId = string.Empty;
    private string _apiVersion = "2024-06-01";
    private InteractiveBrowserCredential? _credential;

    // OpenAI settings
    private string _openAiApiKey = string.Empty;
    private string _openAiModel = "gpt-4o";

    // Claude settings
    private string _claudeApiKey = string.Empty;
    private string _claudeModel = "claude-sonnet-4-20250514";

    // Ollama settings
    private string _ollamaEndpoint = "http://127.0.0.1:11434";
    private string _ollamaModel = "llama3";

    private string _systemPrompt = string.Empty;

    private const string SystemPrompt = """
        You are an expert Revit API programmer embedded inside a Revit 2025 plugin called "RevAI".
        Your job is to translate natural language requests into executable C# code that uses the Revit API.

        IMPORTANT RULES:
        1. You MUST output ONLY valid C# code inside a ```csharp code block. No explanation outside the code block when generating code.
        2. The code must define a static class called `GeneratedCommand` with a static method:
           public static string Execute(UIApplication uiApp)
        3. The method receives a UIApplication instance. Use it to get Document, UIDocument, etc.
        4. The method must return a string describing the result of the operation.
        5. Always wrap model-modifying operations in a Transaction.
        6. Available using directives are pre-imported:
           - Autodesk.Revit.DB
           - Autodesk.Revit.DB.Architecture
           - Autodesk.Revit.DB.Structure
           - Autodesk.Revit.DB.Mechanical
           - Autodesk.Revit.DB.Electrical
           - Autodesk.Revit.DB.Plumbing
           - Autodesk.Revit.UI
           - Autodesk.Revit.UI.Selection
           - System
           - System.Linq
           - System.Collections.Generic
           - System.Text

        COMMON PATTERNS:
        
        // Getting the document
        Document doc = uiApp.ActiveUIDocument.Document;
        UIDocument uidoc = uiApp.ActiveUIDocument;
        
        // Using a Transaction (REQUIRED for any model changes)
        using (Transaction tx = new Transaction(doc, "Description"))
        {
            tx.Start();
            // ... modify model ...
            tx.Commit();
        }
        
        // Filtering elements
        var collector = new FilteredElementCollector(doc);
        var walls = collector.OfClass(typeof(Wall)).ToElements();
        
        // Getting element parameters
        Parameter param = element.get_Parameter(BuiltInParameter.SOME_PARAM);
        // or
        Parameter param = element.LookupParameter("Parameter Name");
        
        // Creating elements
        Wall.Create(doc, line, wallTypeId, levelId, height, offset, false, false);
        
        // Getting levels
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();
        
        // Getting types
        var wallTypes = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .ToList();
        
        // Selecting elements
        var selectedIds = uidoc.Selection.GetElementIds();
        
        // Getting element location
        LocationPoint locPoint = element.Location as LocationPoint;
        LocationCurve locCurve = element.Location as LocationCurve;
        
        // Units: Revit 2025 uses internal units (feet). Convert with UnitUtils.
        double meters = UnitUtils.ConvertFromInternalUnits(feetValue, UnitTypeId.Meters);
        double feet = UnitUtils.ConvertToInternalUnits(meterValue, UnitTypeId.Meters);

        EXAMPLE - Count all walls:
        ```csharp
        public static class GeneratedCommand
        {
            public static string Execute(UIApplication uiApp)
            {
                Document doc = uiApp.ActiveUIDocument.Document;
                var walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .ToElements();
                return $"Found {walls.Count} walls in the model.";
            }
        }
        ```

        EXAMPLE - Create a wall:
        ```csharp
        public static class GeneratedCommand
        {
            public static string Execute(UIApplication uiApp)
            {
                Document doc = uiApp.ActiveUIDocument.Document;
                
                var level = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault();
                    
                if (level == null) return "No levels found in the document.";
                
                var wallType = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .FirstOrDefault();
                    
                if (wallType == null) return "No wall types found in the document.";
                
                using (Transaction tx = new Transaction(doc, "Create Wall"))
                {
                    tx.Start();
                    
                    XYZ start = new XYZ(0, 0, 0);
                    XYZ end = new XYZ(20, 0, 0); // 20 feet
                    Line line = Line.CreateBound(start, end);
                    
                    Wall wall = Wall.Create(doc, line, wallType.Id, level.Id, 10, 0, false, false);
                    
                    tx.Commit();
                    
                    return $"Created wall (Id: {wall.Id}) on level '{level.Name}' with type '{wallType.Name}'.";
                }
            }
        }
        ```

        EXAMPLE - Get info about selected elements:
        ```csharp
        public static class GeneratedCommand
        {
            public static string Execute(UIApplication uiApp)
            {
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;
                
                var selectedIds = uidoc.Selection.GetElementIds();
                if (selectedIds.Count == 0) return "No elements selected.";
                
                var sb = new StringBuilder();
                sb.AppendLine($"Selected {selectedIds.Count} element(s):");
                
                foreach (var id in selectedIds)
                {
                    var elem = doc.GetElement(id);
                    sb.AppendLine($"  - {elem.Name} (Category: {elem.Category?.Name}, Id: {elem.Id})");
                }
                
                return sb.ToString();
            }
        }
        ```

        If the user asks a general question that doesn't require code execution, respond normally in plain text WITHOUT a code block.
        If a previous code execution failed, analyze the error and provide corrected code.
        Always prefer safe operations. Never delete elements unless explicitly asked.
        When modifying elements, always confirm what was changed in the return string.
        """;

    public AiService()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        _systemPrompt = SystemPrompt;
    }

    public void ConfigureCopilot(string endpoint, string deploymentName, string tenantId = "")
    {
        _provider = AiProvider.Copilot;
        _endpoint = endpoint.TrimEnd('/');
        _deploymentName = deploymentName;
        _tenantId = tenantId;

        // Create interactive browser credential for SSO
        var options = new InteractiveBrowserCredentialOptions
        {
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "RevAI" }
        };
        if (!string.IsNullOrWhiteSpace(_tenantId))
            options.TenantId = _tenantId;

        _credential = new InteractiveBrowserCredential(options);
    }

    public void ConfigureOpenAi(string apiKey, string model = "gpt-4o")
    {
        _provider = AiProvider.OpenAI;
        _openAiApiKey = apiKey;
        _openAiModel = model;
    }

    public void ConfigureClaude(string apiKey, string model = "claude-sonnet-4-20250514")
    {
        _provider = AiProvider.Claude;
        _claudeApiKey = apiKey;
        _claudeModel = model;
    }

    public void ConfigureOllama(string endpoint = "http://127.0.0.1:11434", string model = "llama3")
    {
        _provider = AiProvider.Ollama;
        _ollamaEndpoint = endpoint.TrimEnd('/');
        _ollamaModel = model;
    }

    public bool IsConfigured => _provider switch
    {
        AiProvider.Copilot => !string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_deploymentName) && _credential != null,
        AiProvider.OpenAI => !string.IsNullOrEmpty(_openAiApiKey),
        AiProvider.Claude => !string.IsNullOrEmpty(_claudeApiKey),
        AiProvider.Ollama => !string.IsNullOrEmpty(_ollamaEndpoint) && !string.IsNullOrEmpty(_ollamaModel),
        _ => false
    };

    private async Task EnsureAuthHeaderAsync()
    {
        if (_credential == null) throw new InvalidOperationException("SSO credential not configured.");

        var tokenResult = await _credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(["https://cognitiveservices.azure.com/.default"]));

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResult.Token);
    }

    public async Task<AiResponse> SendMessageAsync(string userMessage)
    {
        if (!IsConfigured)
            return new AiResponse("Please configure your AI provider settings in Settings first.", null);

        _conversationHistory.Add(new AiMessage("user", userMessage));

        try
        {
            return _provider switch
            {
                AiProvider.Copilot => await SendViaCopilotAsync(),
                AiProvider.OpenAI => await SendViaOpenAiAsync(),
                AiProvider.Claude => await SendViaClaudeAsync(),
                AiProvider.Ollama => await SendViaOllamaAsync(),
                _ => new AiResponse("Unknown AI provider.", null)
            };
        }
        catch (AuthenticationFailedException ex)
        {
            return new AiResponse($"SSO Authentication failed: {ex.Message}\n\nPlease check your Tenant ID and try again.", null);
        }
        catch (Exception ex)
        {
            return new AiResponse($"Error communicating with AI service: {ex.Message}", null);
        }
    }

    private async Task<AiResponse> SendViaCopilotAsync()
    {
        var messages = BuildOpenAiMessages();
        var requestBody = new { messages, max_tokens = 4096, temperature = 0.2 };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await EnsureAuthHeaderAsync();

        var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}";
        var response = await _httpClient.PostAsync(url, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new AiResponse($"API Error ({response.StatusCode}): {responseJson}", null);

        var result = JsonSerializer.Deserialize<AzureOpenAiResponse>(responseJson);
        var assistantMessage = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response from AI.";

        _conversationHistory.Add(new AiMessage("assistant", assistantMessage));
        return new AiResponse(assistantMessage, ExtractCodeBlock(assistantMessage));
    }

    private async Task<AiResponse> SendViaOpenAiAsync()
    {
        var messages = BuildOpenAiMessages();
        var requestBody = new { model = _openAiModel, messages, max_tokens = 4096, temperature = 0.2 };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiApiKey);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new AiResponse($"OpenAI API Error ({response.StatusCode}): {responseJson}", null);

        var result = JsonSerializer.Deserialize<AzureOpenAiResponse>(responseJson);
        var assistantMessage = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response from AI.";

        _conversationHistory.Add(new AiMessage("assistant", assistantMessage));
        return new AiResponse(assistantMessage, ExtractCodeBlock(assistantMessage));
    }

    private async Task<AiResponse> SendViaClaudeAsync()
    {
        var messages = _conversationHistory.Select(m => new { role = m.Role, content = m.Content }).ToList();

        var requestBody = new
        {
            model = _claudeModel,
            max_tokens = 4096,
            system = _systemPrompt,
            messages
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _claudeApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new AiResponse($"Claude API Error ({response.StatusCode}): {responseJson}", null);

        var result = JsonSerializer.Deserialize<ClaudeResponse>(responseJson);
        var assistantMessage = result?.Content?.FirstOrDefault()?.Text ?? "No response from AI.";

        _conversationHistory.Add(new AiMessage("assistant", assistantMessage));
        return new AiResponse(assistantMessage, ExtractCodeBlock(assistantMessage));
    }

    private async Task<AiResponse> SendViaOllamaAsync()
    {
        var messages = BuildOpenAiMessages();
        var requestBody = new { model = _ollamaModel, messages, stream = false };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"{_ollamaEndpoint}/api/chat";
        var response = await _httpClient.PostAsync(url, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new AiResponse($"Ollama API Error ({response.StatusCode}): {responseJson}", null);

        var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson);
        var assistantMessage = result?.Message?.Content ?? "No response from Ollama.";

        _conversationHistory.Add(new AiMessage("assistant", assistantMessage));
        return new AiResponse(assistantMessage, ExtractCodeBlock(assistantMessage));
    }

    private List<object> BuildOpenAiMessages()
    {
        var messages = new List<object>
        {
            new { role = "system", content = _systemPrompt }
        };
        messages.AddRange(_conversationHistory.Select(m => (object)new { role = m.Role, content = m.Content }));
        return messages;
    }

    public void AddExecutionResult(string result, bool success)
    {
        var prefix = success ? "Code executed successfully" : "Code execution failed";
        _conversationHistory.Add(new AiMessage("user", $"[{prefix}] Output:\n{result}"));
    }

    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }

    public void SignOut()
    {
        _credential = null;
    }

    private static string? ExtractCodeBlock(string text)
    {
        const string csharpMarker = "```csharp";
        const string endMarker = "```";

        int startIdx = text.IndexOf(csharpMarker, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0) return null;

        startIdx += csharpMarker.Length;
        int endIdx = text.IndexOf(endMarker, startIdx, StringComparison.OrdinalIgnoreCase);
        if (endIdx < 0) return null;

        return text[startIdx..endIdx].Trim();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public record AiMessage(string Role, string Content);

public record AiResponse(string FullResponse, string? Code)
{
    public bool HasCode => !string.IsNullOrEmpty(Code);
}

// JSON response models for Azure OpenAI API
public class AzureOpenAiResponse
{
    [JsonPropertyName("choices")]
    public List<AzureOpenAiChoice>? Choices { get; set; }
}

public class AzureOpenAiChoice
{
    [JsonPropertyName("message")]
    public AzureOpenAiMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class AzureOpenAiMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

// JSON response models for Claude (Anthropic) API
public class ClaudeResponse
{
    [JsonPropertyName("content")]
    public List<ClaudeContentBlock>? Content { get; set; }
}

public class ClaudeContentBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

// JSON response models for Ollama API
public class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; set; }
}

public class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
