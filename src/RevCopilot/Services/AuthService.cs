using Microsoft.Identity.Client;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace RevCopilot.Services;

/// <summary>
/// Handles Microsoft 365 authentication via MSAL (Microsoft Authentication Library).
/// Uses the Authorization Code + PKCE flow with encrypted token cache.
///
/// Azure AD app registration requirements:
///   • Platform: Mobile and desktop applications
///   • Redirect URI: http://localhost
///   • Delegated permissions: User.Read, Chat.ReadWrite, offline_access
/// </summary>
public class AuthService : IDisposable
{
    // -----------------------------------------------------------------------
    // Scopes requested from Microsoft Graph
    // -----------------------------------------------------------------------
    private static readonly string[] GraphScopes =
    [
        "https://graph.microsoft.com/User.Read",
        "https://graph.microsoft.com/Chat.ReadWrite",
        "offline_access"
    ];

    // -----------------------------------------------------------------------
    // Encrypted token cache path
    // -----------------------------------------------------------------------
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RevCopilot", "msal_token_cache.bin");

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------
    private IPublicClientApplication? _msalClient;
    private string _clientId = string.Empty;
    private string _tenantId = "common";
    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId);
    public string? UserDisplayName { get; private set; }
    public string? UserEmail { get; private set; }
    public bool IsSignedIn =>
        !string.IsNullOrEmpty(_cachedAccessToken) &&
        _tokenExpiry > DateTimeOffset.UtcNow.AddMinutes(5);

    // -----------------------------------------------------------------------
    // Configuration
    // -----------------------------------------------------------------------

    /// <summary>Call whenever the user saves new settings.</summary>
    public void Configure(string clientId, string tenantId)
    {
        if (clientId == _clientId && tenantId == _tenantId && _msalClient != null)
            return;

        _clientId = clientId;
        _tenantId = string.IsNullOrWhiteSpace(tenantId) ? "common" : tenantId;
        _cachedAccessToken = null;
        _tokenExpiry = DateTimeOffset.MinValue;

        BuildMsalClient();
    }

    private void BuildMsalClient()
    {
        if (string.IsNullOrWhiteSpace(_clientId)) return;

        // Determine authority: "common" allows personal + work accounts;
        // a specific tenant GUID limits to that organisation.
        var authority = _tenantId.Equals("common", StringComparison.OrdinalIgnoreCase)
            ? "https://login.microsoftonline.com/common"
            : $"https://login.microsoftonline.com/{_tenantId}";

        _msalClient = PublicClientApplicationBuilder
            .Create(_clientId)
            .WithAuthority(authority)
            .WithDefaultRedirectUri()   // uses http://localhost
            .Build();

        AttachTokenCache();
    }

    private void AttachTokenCache()
    {
        if (_msalClient == null) return;

        Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);

        _msalClient.UserTokenCache.SetBeforeAccessAsync(async args =>
        {
            if (File.Exists(CacheFilePath))
            {
                try
                {
                    var cipher = await File.ReadAllBytesAsync(CacheFilePath);
                    var plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                    args.TokenCache.DeserializeMsalV3(plain);
                }
                catch
                {
                    // Corrupted cache — start fresh
                    File.Delete(CacheFilePath);
                }
            }
        });

        _msalClient.UserTokenCache.SetAfterAccessAsync(async args =>
        {
            if (args.HasStateChanged)
            {
                var plain = args.TokenCache.SerializeMsalV3();
                var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                await File.WriteAllBytesAsync(CacheFilePath, cipher);
            }
        });
    }

    // -----------------------------------------------------------------------
    // Token acquisition
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns a valid access token. Attempts silent refresh first;
    /// falls back to interactive browser sign-in.
    /// </summary>
    /// <param name="parentWindowHandle">
    /// Win32 HWND of the host window. Required when running inside Revit so the
    /// authentication browser popup is correctly parented (preventing it from
    /// appearing behind the main window or being lost).
    /// Pass <see cref="IntPtr.Zero"/> to let MSAL choose.
    /// </param>
    public async Task<string> GetAccessTokenAsync(
        IntPtr parentWindowHandle = default,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "RevCopilot is not configured.\nPlease open ⚙ Settings and enter your Azure AD Client ID.");

        if (_msalClient == null) BuildMsalClient();

        // 1. Try to use an already-valid cached token
        if (IsSignedIn) return _cachedAccessToken!;

        // 2. Try silent acquisition from the MSAL token cache
        var accounts = await _msalClient!.GetAccountsAsync();
        var firstAccount = accounts.FirstOrDefault();

        try
        {
            if (firstAccount != null)
            {
                var silentResult = await _msalClient
                    .AcquireTokenSilent(GraphScopes, firstAccount)
                    .ExecuteAsync(cancellationToken);

                StoreResult(silentResult);
                _ = FetchUserProfileAsync(silentResult.AccessToken); // fire-and-forget
                return _cachedAccessToken!;
            }
        }
        catch (MsalUiRequiredException)
        {
            // Fall through to interactive flow
        }

        // 3. Interactive sign-in — open system browser.
        //    WithParentActivityOrWindow is required inside Revit's Win32 host;
        //    without it the auth popup appears behind Revit and looks frozen.
        var builder = _msalClient
            .AcquireTokenInteractive(GraphScopes)
            .WithPrompt(Prompt.SelectAccount);

        if (parentWindowHandle != IntPtr.Zero)
            builder = builder.WithParentActivityOrWindow(parentWindowHandle);

        var interactiveResult = await builder.ExecuteAsync(cancellationToken);

        StoreResult(interactiveResult);
        await FetchUserProfileAsync(interactiveResult.AccessToken);
        return _cachedAccessToken!;
    }

    private void StoreResult(AuthenticationResult result)
    {
        _cachedAccessToken = result.AccessToken;
        _tokenExpiry = result.ExpiresOn;
    }

    // -----------------------------------------------------------------------
    // User profile
    // -----------------------------------------------------------------------

    private async Task FetchUserProfileAsync(string accessToken)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var json = await http.GetStringAsync(
                "https://graph.microsoft.com/v1.0/me?$select=displayName,mail,userPrincipalName");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            UserDisplayName =
                root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;

            UserEmail =
                (root.TryGetProperty("mail", out var m) && m.GetString() is { } mail && !string.IsNullOrEmpty(mail))
                    ? mail
                    : root.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() : null;
        }
        catch
        {
            // Profile fetch is non-critical — swallow
        }
    }

    // -----------------------------------------------------------------------
    // Sign-out
    // -----------------------------------------------------------------------

    public async Task SignOutAsync()
    {
        if (_msalClient != null)
        {
            var accounts = await _msalClient.GetAccountsAsync();
            foreach (var account in accounts)
                await _msalClient.RemoveAsync(account);
        }

        _cachedAccessToken = null;
        _tokenExpiry = DateTimeOffset.MinValue;
        UserDisplayName = null;
        UserEmail = null;

        if (File.Exists(CacheFilePath))
            File.Delete(CacheFilePath);
    }

    public void Dispose() { }
}
