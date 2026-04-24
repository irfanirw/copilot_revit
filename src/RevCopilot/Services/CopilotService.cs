using RevCopilot.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RevCopilot.Services;

/// <summary>
/// Calls the Microsoft 365 Copilot via Microsoft Graph API (beta).
///
/// Flow per conversation session:
///   1. POST /beta/copilot/chats              → create a chat thread (optionally targeting a specific agent)
///   2. POST /beta/copilot/chats/{id}/messages → send a user message
///   3. GET  /beta/copilot/chats/{id}/messages  → poll for the Copilot reply
///
/// Permissions needed on the Azure AD app:
///   Chat.ReadWrite (delegated)
///
/// Note: The /beta/copilot/* endpoints are in preview. If your tenant does not yet
/// have them enabled, the service surfaces a clear error message.
/// </summary>
public class CopilotService : IDisposable
{
    private const string GraphBeta = "https://graph.microsoft.com/beta";

    // Polling config for async Copilot responses
    private const int PollIntervalMs = 2_000;
    private const int PollMaxAttempts = 20; // 40 seconds max

    private readonly AuthService _authService;
    private readonly HttpClient _httpClient = new();

    private string? _chatId;            // Current Graph chat thread
    private string? _selectedAgentId;  // null = default M365 Copilot
    private string? _lastMessageId;     // ID of the last message we sent (for polling)

    public string? SelectedAgentId
    {
        get => _selectedAgentId;
        set
        {
            if (_selectedAgentId == value) return;
            _selectedAgentId = value;
            _chatId = null;         // New agent → start a fresh thread
            _lastMessageId = null;
        }
    }

    private IntPtr _parentWindowHandle = IntPtr.Zero;

    public void SetParentWindowHandle(IntPtr handle) => _parentWindowHandle = handle;

    public CopilotService(AuthService authService)
    {
        _authService = authService;
    }

    // -----------------------------------------------------------------------
    // Chat
    // -----------------------------------------------------------------------

    /// <summary>Sends a user message and returns Copilot's reply text.</summary>
    public async Task<string> SendMessageAsync(string userMessage,
                                               CancellationToken cancellationToken = default)
    {
        await SetBearerTokenAsync(cancellationToken);

        // Ensure a chat thread exists for this session
        if (_chatId == null)
            _chatId = await CreateChatAsync(cancellationToken);

        // Send the user message
        var sentMessageId = await PostUserMessageAsync(_chatId, userMessage, cancellationToken);
        _lastMessageId = sentMessageId;

        // Poll for the Copilot reply
        var reply = await PollForReplyAsync(_chatId, sentMessageId, cancellationToken);
        return reply;
    }

    /// <summary>Starts a new chat session (new thread).</summary>
    public void NewConversation()
    {
        _chatId = null;
        _lastMessageId = null;
    }

    // -----------------------------------------------------------------------
    // Agent discovery
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the list of Copilot agents available in the tenant.
    /// Uses GET /beta/copilot/managedAgents.
    /// </summary>
    public async Task<List<CopilotAgent>> GetAvailableAgentsAsync(
        CancellationToken cancellationToken = default)
    {
        await SetBearerTokenAsync(cancellationToken);

        var agents = new List<CopilotAgent>
        {
            // Always include the default M365 Copilot entry
            new() { Id = string.Empty, DisplayName = "M365 Copilot (Business Chat)", IsDefault = true }
        };

        try
        {
            var response = await _httpClient.GetAsync(
                $"{GraphBeta}/copilot/managedAgents", cancellationToken);

            if (!response.IsSuccessStatusCode) return agents;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var arr)) return agents;

            foreach (var item in arr.EnumerateArray())
            {
                agents.Add(new CopilotAgent
                {
                    Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    DisplayName = item.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "",
                    Description = item.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : ""
                });
            }
        }
        catch
        {
            // Non-critical — just return the default entry
        }

        return agents;
    }

    // -----------------------------------------------------------------------
    // Private: Graph API helpers
    // -----------------------------------------------------------------------

    private async Task SetBearerTokenAsync(CancellationToken ct)
    {
        var token = await _authService.GetAccessTokenAsync(_parentWindowHandle, ct);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Creates a new Copilot chat thread.
    /// POST /beta/copilot/chats
    /// </summary>
    private async Task<string> CreateChatAsync(CancellationToken ct)
    {
        object body = string.IsNullOrEmpty(_selectedAgentId)
            ? new { }
            : new { agentId = _selectedAgentId };

        var content = JsonContent(body);
        var response = await _httpClient.PostAsync($"{GraphBeta}/copilot/chats", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Failed to create Copilot chat ({(int)response.StatusCode}).\n\n" +
                $"This usually means:\n" +
                $"  • The beta Copilot API is not yet available in your tenant, OR\n" +
                $"  • Your Azure AD app is missing Chat.ReadWrite permission.\n\n" +
                $"Details: {err}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Posts a user message to the chat thread.
    /// POST /beta/copilot/chats/{chatId}/messages
    /// Returns the new message ID.
    /// </summary>
    private async Task<string> PostUserMessageAsync(string chatId, string text, CancellationToken ct)
    {
        var body = new
        {
            body = new { content = text, contentType = "text" }
        };

        var content = JsonContent(body);
        var response = await _httpClient.PostAsync(
            $"{GraphBeta}/copilot/chats/{chatId}/messages", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Failed to send message ({(int)response.StatusCode}): {err}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Polls for the Copilot reply that follows the sent message.
    /// GET /beta/copilot/chats/{chatId}/messages
    /// </summary>
    private async Task<string> PollForReplyAsync(string chatId, string sentMessageId,
                                                  CancellationToken ct)
    {
        for (var attempt = 0; attempt < PollMaxAttempts; attempt++)
        {
            await Task.Delay(PollIntervalMs, ct);

            var response = await _httpClient.GetAsync(
                $"{GraphBeta}/copilot/chats/{chatId}/messages" +
                "?$orderby=createdDateTime%20desc&$top=10", ct);

            if (!response.IsSuccessStatusCode) continue;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var messages)) continue;

            // Walk messages newest-first looking for a Copilot reply after our sent message
            foreach (var msg in messages.EnumerateArray())
            {
                var msgId = msg.TryGetProperty("id", out var id) ? id.GetString() : null;
                if (msgId == sentMessageId) break; // reached our own message → no reply yet

                // Check sender is not the user (i.e. it is the assistant / Copilot)
                var fromProp = msg.TryGetProperty("from", out var from) ? from : (JsonElement?)null;
                var isUser = fromProp.HasValue &&
                             fromProp.Value.TryGetProperty("user", out _);
                if (isUser) continue;

                // Extract the message body
                if (!msg.TryGetProperty("body", out var bodyEl)) continue;
                var contentType = bodyEl.TryGetProperty("contentType", out var ct2)
                    ? ct2.GetString() : "text";
                var text = bodyEl.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(text)) continue;

                // Strip HTML if the response comes back as html
                if (contentType == "html")
                    text = StripHtml(text);

                return text;
            }
        }

        throw new TimeoutException(
            "Copilot did not respond within the expected time. Please try again.");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static StringContent JsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>Very lightweight HTML → plain text strip (no external dependency).</summary>
    private static string StripHtml(string html)
    {
        var sb = new StringBuilder();
        bool inTag = false;

        foreach (char c in html)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag) sb.Append(c);
        }

        // Decode common entities
        return sb.ToString()
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&nbsp;", " ")
            .Replace("&#39;", "'")
            .Replace("&quot;", "\"")
            .Trim();
    }

    public void Dispose() => _httpClient.Dispose();
}
