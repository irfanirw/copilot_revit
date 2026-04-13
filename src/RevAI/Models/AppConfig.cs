using System.Text.Json.Serialization;

namespace RevAI.Models;

public enum AiProvider
{
    Copilot,
    OpenAI,
    Claude,
    Ollama
}

public class AppConfig
{
    [JsonPropertyName("provider")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AiProvider Provider { get; set; } = AiProvider.Copilot;

    // Azure OpenAI / Copilot settings
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("deploymentName")]
    public string DeploymentName { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    // OpenAI settings
    [JsonPropertyName("openAiApiKey")]
    public string OpenAiApiKey { get; set; } = string.Empty;

    [JsonPropertyName("openAiModel")]
    public string OpenAiModel { get; set; } = "gpt-4o";

    // Claude settings
    [JsonPropertyName("claudeApiKey")]
    public string ClaudeApiKey { get; set; } = string.Empty;

    [JsonPropertyName("claudeModel")]
    public string ClaudeModel { get; set; } = "claude-sonnet-4-20250514";

    // Ollama settings
    [JsonPropertyName("ollamaEndpoint")]
    public string OllamaEndpoint { get; set; } = "http://127.0.0.1:11434";

    [JsonPropertyName("ollamaModel")]
    public string OllamaModel { get; set; } = "llama3";
}
