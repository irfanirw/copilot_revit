namespace RevCopilot.Models;

/// <summary>
/// Represents a Microsoft 365 Copilot agent available in the tenant.
/// Populated from GET /beta/copilot/managedAgents.
/// </summary>
public class CopilotAgent
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>True when this represents the default M365 Copilot Business Chat.</summary>
    public bool IsDefault { get; set; }

    public override string ToString() => DisplayName;
}
