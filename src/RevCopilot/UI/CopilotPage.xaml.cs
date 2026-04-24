using Autodesk.Revit.UI;
using RevCopilot.Models;
using RevCopilot.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RevCopilot.UI;

public partial class CopilotPage : Page, IDockablePaneProvider, INotifyPropertyChanged
{
    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------
    private UIApplication? _uiApp;
    private AuthService?   _authService;
    private CopilotService? _copilotService;

    private readonly ObservableCollection<ChatMessage> _messages = [];
    private bool _isInitialized;
    private bool _isProcessing;
    private bool _isSettingsVisible;
    private bool _isSignInPromptVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    // -----------------------------------------------------------------------
    // Bindable properties
    // -----------------------------------------------------------------------

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); }
    }

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set { _isSettingsVisible = value; OnPropertyChanged(); }
    }

    public bool IsSignInPromptVisible
    {
        get => _isSignInPromptVisible;
        set
        {
            _isSignInPromptVisible = value;
            OnPropertyChanged();
            // Manually toggle the messages scroll viewer — avoids a converter for inverse bool→vis
            if (ChatScrollViewer != null)
                ChatScrollViewer.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // -----------------------------------------------------------------------
    // Config paths
    // -----------------------------------------------------------------------
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevCopilot");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    // -----------------------------------------------------------------------
    // Dockable pane setup
    // -----------------------------------------------------------------------

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        data.FrameworkElement = this;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right
        };
    }

    public CopilotPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    // -----------------------------------------------------------------------
    // Initialisation (called once on first command execution)
    // -----------------------------------------------------------------------

    public void Initialize(UIApplication uiApp)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _uiApp = uiApp;
        _authService    = new AuthService();
        _copilotService = new CopilotService(_authService);

        MessagesPanel.ItemsSource = _messages;

        // Restore saved config
        var config = LoadConfig();
        ClientIdBox.Text  = config.ClientId;
        TenantIdBox.Text  = config.TenantId;

        if (string.IsNullOrWhiteSpace(config.ClientId))
        {
            // No app registered yet — show settings immediately
            IsSettingsVisible = true;
            IsSignInPromptVisible = false; // show message area with welcome msg instead
            AddSystemMessage(
                "👋 Welcome to RevCopilot!\n\n" +
                "To get started:\n" +
                "1. Open ⚙ Settings\n" +
                "2. Register an app in Azure AD (portal.azure.com)\n" +
                "3. Enter your Client ID and Tenant ID\n" +
                "4. Click 'Save & Sign In'\n\n" +
                "You will be redirected to your browser to sign in with your M365 account.");
            IsSignInPromptVisible = false;
        }
        else
        {
            _authService.Configure(config.ClientId, config.TenantId);
            if (!string.IsNullOrEmpty(config.DefaultAgentId))
                _copilotService.SelectedAgentId = config.DefaultAgentId;

            UpdateUserInfoDisplay();

            if (_authService.IsSignedIn)
                ShowChatReady();
            else
                IsSignInPromptVisible = true;
        }
    }

    // -----------------------------------------------------------------------
    // Header button handlers
    // -----------------------------------------------------------------------

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        IsSettingsVisible = !IsSettingsVisible;
        if (IsSettingsVisible)
            _ = LoadAgentsIntoComboAsync(); // fire-and-forget intentionally
    }

    private void NewChat_Click(object sender, RoutedEventArgs e)
    {
        _copilotService?.NewConversation();
        _messages.Clear();
        AddSystemMessage("New conversation started. How can Copilot help you today?");
    }

    // -----------------------------------------------------------------------
    // Settings handlers
    // -----------------------------------------------------------------------

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var clientId = ClientIdBox.Text.Trim();
        var tenantId = TenantIdBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(clientId))
        {
            AddSystemMessage("⚠ Please enter a Client ID before saving.");
            return;
        }

        var selectedAgent = AgentComboBox.SelectedItem as CopilotAgent;

        var config = new AppConfig
        {
            ClientId      = clientId,
            TenantId      = string.IsNullOrWhiteSpace(tenantId) ? "common" : tenantId,
            DefaultAgentId = selectedAgent?.IsDefault == false ? selectedAgent.Id : string.Empty
        };

        SaveConfig(config);

        _authService!.Configure(config.ClientId, config.TenantId);
        _copilotService!.SelectedAgentId = config.DefaultAgentId;
        _copilotService.NewConversation();

        IsSettingsVisible = false;

        await SignInAsync();
    }

    private async void SignOut_Click(object sender, RoutedEventArgs e)
    {
        if (_authService == null) return;

        await _authService.SignOutAsync();
        _messages.Clear();
        IsSignInPromptVisible = true;
        UpdateUserInfoDisplay();
        AddSystemMessage("You have signed out.");
        IsSignInPromptVisible = false;
    }

    private async void RefreshAgents_Click(object sender, RoutedEventArgs e)
    {
        await LoadAgentsIntoComboAsync();
    }

    private void AgentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_copilotService == null) return;
        if (AgentComboBox.SelectedItem is CopilotAgent agent)
        {
            _copilotService.SelectedAgentId = agent.IsDefault ? null : agent.Id;
            _copilotService.NewConversation();
        }
    }

    // -----------------------------------------------------------------------
    // Sign-in prompt
    // -----------------------------------------------------------------------

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        await SignInAsync();
    }

    private async Task SignInAsync()
    {
        if (_authService == null) return;

        if (!_authService.IsConfigured)
        {
            IsSettingsVisible = true;
            AddSystemMessage("⚠ Please configure your Azure AD app in Settings before signing in.");
            return;
        }

        IsProcessing = true;
        StatusText.Text = "Opening sign-in browser…";

        try
        {
            await _authService.GetAccessTokenAsync();
            UpdateUserInfoDisplay();
            ShowChatReady();
        }
        catch (Exception ex)
        {
            AddSystemMessage($"❌ Sign-in failed: {ex.Message}");
            IsSignInPromptVisible = true;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    // -----------------------------------------------------------------------
    // Chat
    // -----------------------------------------------------------------------

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessageAsync();

    private async void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await SendMessageAsync();
        }
    }

    private async Task SendMessageAsync()
    {
        var text = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(text) || IsProcessing || _copilotService == null) return;

        InputBox.Text = string.Empty;
        IsProcessing  = true;

        _messages.Add(new ChatMessage(MessageRole.User, text));
        ScrollToBottom();

        try
        {
            StatusText.Text = "Copilot is thinking…";
            var reply = await _copilotService.SendMessageAsync(text);

            _messages.Add(new ChatMessage(MessageRole.Assistant, reply));
        }
        catch (Exception ex)
        {
            _messages.Add(new ChatMessage(MessageRole.System, $"❌ {ex.Message}"));
        }
        finally
        {
            IsProcessing = false;
            ScrollToBottom();
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void ShowChatReady()
    {
        IsSignInPromptVisible = false;
        _messages.Clear();
        AddSystemMessage(
            $"✅ Signed in as {_authService!.UserDisplayName ?? _authService.UserEmail ?? "unknown"}.\n\n" +
            "You can now chat with Microsoft 365 Copilot. Ask anything — about your documents, " +
            "emails, tasks, or anything else Copilot can help with.\n\n" +
            "Tip: Use ⚙ Settings to switch to a specific Copilot Studio agent.");
    }

    private void AddSystemMessage(string text) =>
        _messages.Add(new ChatMessage(MessageRole.System, text));

    private void UpdateUserInfoDisplay()
    {
        if (_authService == null) return;
        UserInfoText.Text = _authService.IsSignedIn
            ? _authService.UserEmail ?? _authService.UserDisplayName ?? "Signed in"
            : "Not signed in";
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(() => ChatScrollViewer?.ScrollToBottom());
    }

    private async Task LoadAgentsIntoComboAsync()
    {
        if (_copilotService == null || _authService == null) return;
        if (!_authService.IsSignedIn) return;

        try
        {
            var agents = await _copilotService.GetAvailableAgentsAsync();
            AgentComboBox.ItemsSource  = agents;
            AgentComboBox.SelectedIndex = 0;

            // Re-select previously configured agent
            var config = LoadConfig();
            if (!string.IsNullOrEmpty(config.DefaultAgentId))
            {
                var match = agents.FirstOrDefault(a => a.Id == config.DefaultAgentId);
                if (match != null) AgentComboBox.SelectedItem = match;
            }
        }
        catch
        {
            // Non-critical
        }
    }

    // -----------------------------------------------------------------------
    // Config persistence
    // -----------------------------------------------------------------------

    private static AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    private static void SaveConfig(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(config,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
