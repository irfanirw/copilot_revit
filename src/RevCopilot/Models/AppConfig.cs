using System.Text.Json.Serialization;

namespace RevCopilot.Models;

/// <summary>
/// Persisted configuration for RevCopilot, stored as JSON in %APPDATA%/RevCopilot/config.json.
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Azure AD Tenant ID. Use "common" for multi-tenant / personal accounts,
    /// or your organisation's tenant GUID / domain (e.g. "contoso.onmicrosoft.com").
    /// </summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = "common";

    /// <summary>
    /// Client ID of the Azure AD app registration.
    /// The app must have these delegated permissions:
    ///   • User.Read
    ///   • Chat.ReadWrite
    ///   • offline_access
    /// Redirect URI: http://localhost  (Mobile and desktop applications platform)
    /// </summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Optional: ID of the Copilot Studio agent to use by default.
    /// Leave empty to use M365 Copilot Business Chat.
    /// </summary>
    [JsonPropertyName("defaultAgentId")]
    public string DefaultAgentId { get; set; } = string.Empty;
}
