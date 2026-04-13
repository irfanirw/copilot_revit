using Autodesk.Revit.UI;
using RevAI.Models;
using RevAI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace RevAI.UI;

public partial class ChatWindow : Window, INotifyPropertyChanged
{
    private readonly UIApplication _uiApp;
    private readonly AiService _aiService;
    private readonly ObservableCollection<ChatMessage> _messages = [];
    private bool _isProcessing;
    private bool _isSettingsVisible;

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public ChatWindow(UIApplication uiApp)
    {
        InitializeComponent();
        DataContext = this;

        _uiApp = uiApp;
        _aiService = new AiService();

        MessagesPanel.ItemsSource = _messages;

        // Handle Ctrl+C/V/A for Revit-hosted WPF (Revit intercepts keyboard shortcuts)
        PreviewKeyDown += ChatWindow_PreviewKeyDown;

        // Load saved settings
        LoadSettings();

        // Welcome message
        _messages.Add(new ChatMessage(
            "assistant",
            "Welcome to **RevAI**! 🤖\n\n" +
            "I'm your AI-powered Revit assistant. Choose your preferred AI provider:\n" +
            "• **M365 Copilot** (Azure OpenAI with SSO)\n" +
            "• **OpenAI ChatGPT** (API key)\n" +
            "• **Anthropic Claude** (API key)\n" +
            "• **Ollama** (offline, local model)\n\n" +
            "Tell me what you want to do in natural language, and I'll generate and execute the Revit API code for you.\n\n" +
            "**Examples:**\n" +
            "• \"Count all walls in the model\"\n" +
            "• \"Create a 10m wall on Level 1\"\n" +
            "• \"List all rooms with their areas\"\n\n" +
            "⚙️ Click **Settings** to configure your AI provider."));

        if (!_aiService.IsConfigured)
        {
            _messages.Add(new ChatMessage("system", "⚠️ Not configured. Please open Settings to select your AI provider and enter credentials."));
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessage();
    
    private async void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await SendMessage();
        }
    }

    private async Task SendMessage()
    {
        var input = InputBox.Text?.Trim();
        if (string.IsNullOrEmpty(input) || IsProcessing) return;

        InputBox.Text = string.Empty;
        IsProcessing = true;

        // Add user message
        _messages.Add(new ChatMessage("user", input));
        ScrollToBottom();

        try
        {
            // Step 1: Send to AI
            StatusText.Text = "Thinking...";
            var response = await _aiService.SendMessageAsync(input);

            if (response.HasCode)
            {
                // Show the AI's response (with code)
                _messages.Add(new ChatMessage("assistant", response.FullResponse));
                ScrollToBottom();

                // Step 2: Execute the generated code in Revit context
                StatusText.Text = "Executing in Revit...";
                await ExecuteCodeInRevit(response.Code!);
            }
            else
            {
                // Pure text response (no code to execute)
                _messages.Add(new ChatMessage("assistant", response.FullResponse));
            }
        }
        catch (Exception ex)
        {
            _messages.Add(new ChatMessage("system", $"❌ Error: {ex.Message}"));
        }
        finally
        {
            IsProcessing = false;
            ScrollToBottom();
        }
    }

    private async Task ExecuteCodeInRevit(string code)
    {
        var handler = App.ExecutionHandler;
        var externalEvent = App.ExternalEvent;

        if (handler == null || externalEvent == null)
        {
            _messages.Add(new ChatMessage("system", "❌ External event handler not available."));
            return;
        }

        var tcs = new TaskCompletionSource<(string Result, bool Success)>();

        // Set the code and callback
        handler.SetCode(code, (result, success) =>
        {
            // Marshal back to the UI thread
            Dispatcher.BeginInvoke(() => tcs.TrySetResult((result, success)));
        });

        // Raise the external event to execute on Revit's thread
        externalEvent.Raise();

        // Wait for the result (with timeout)
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(60)));

        if (completedTask == tcs.Task)
        {
            var (result, success) = tcs.Task.Result;

            if (success)
            {
                _messages.Add(new ChatMessage("result", $"✅ **Execution Result:**\n{result}"));
                _aiService.AddExecutionResult(result, true);
            }
            else
            {
                _messages.Add(new ChatMessage("error", $"❌ **Execution Error:**\n{result}"));
                _aiService.AddExecutionResult(result, false);

                // Auto-retry: ask AI to fix the error
                _messages.Add(new ChatMessage("system", "🔄 Asking AI to fix the error..."));
                ScrollToBottom();

                StatusText.Text = "AI is fixing the error...";
                var fixResponse = await _aiService.SendMessageAsync(
                    "The code failed with the error above. Please provide corrected code.");

                if (fixResponse.HasCode)
                {
                    _messages.Add(new ChatMessage("assistant", fixResponse.FullResponse));
                    ScrollToBottom();

                    StatusText.Text = "Re-executing fixed code...";
                    await ExecuteCodeInRevit(fixResponse.Code!);
                }
                else
                {
                    _messages.Add(new ChatMessage("assistant", fixResponse.FullResponse));
                }
            }
        }
        else
        {
            _messages.Add(new ChatMessage("system", "⏰ Execution timed out after 60 seconds."));
        }
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            ChatScrollViewer.ScrollToEnd();
        });
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        IsSettingsVisible = true;
    }

    private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CopilotSettingsPanel == null) return; // Not yet initialized

        var selected = (ProviderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Copilot";

        CopilotSettingsPanel.Visibility = selected == "Copilot" ? Visibility.Visible : Visibility.Collapsed;
        OpenAiSettingsPanel.Visibility = selected == "OpenAI" ? Visibility.Visible : Visibility.Collapsed;
        ClaudeSettingsPanel.Visibility = selected == "Claude" ? Visibility.Visible : Visibility.Collapsed;
        OllamaSettingsPanel.Visibility = selected == "Ollama" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var selectedProvider = (ProviderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Copilot";
        var provider = Enum.Parse<AiProvider>(selectedProvider);
        string providerName;

        switch (provider)
        {
            case AiProvider.Copilot:
                var endpoint = EndpointBox.Text?.Trim() ?? "";
                var deployment = DeploymentBox.Text?.Trim() ?? "";
                var tenantId = TenantIdBox.Text?.Trim() ?? "";

                if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deployment))
                {
                    _messages.Add(new ChatMessage("system", "⚠️ Endpoint and Deployment Name are required."));
                    ScrollToBottom();
                    return;
                }

                _aiService.ConfigureCopilot(endpoint, deployment, tenantId);
                providerName = "M365 Copilot (Azure OpenAI)";
                break;

            case AiProvider.OpenAI:
                var openAiKey = OpenAiApiKeyBox.Password?.Trim() ?? "";
                var openAiModel = OpenAiModelBox.Text?.Trim() ?? "gpt-4o";

                if (string.IsNullOrEmpty(openAiKey))
                {
                    _messages.Add(new ChatMessage("system", "⚠️ OpenAI API Key is required."));
                    ScrollToBottom();
                    return;
                }

                _aiService.ConfigureOpenAi(openAiKey, openAiModel);
                providerName = "OpenAI ChatGPT";
                break;

            case AiProvider.Claude:
                var claudeKey = ClaudeApiKeyBox.Password?.Trim() ?? "";
                var claudeModel = ClaudeModelBox.Text?.Trim() ?? "claude-sonnet-4-20250514";

                if (string.IsNullOrEmpty(claudeKey))
                {
                    _messages.Add(new ChatMessage("system", "⚠️ Claude API Key is required."));
                    ScrollToBottom();
                    return;
                }

                _aiService.ConfigureClaude(claudeKey, claudeModel);
                providerName = "Anthropic Claude";
                break;

            case AiProvider.Ollama:
                var ollamaEndpoint = OllamaEndpointBox.Text?.Trim() ?? "http://127.0.0.1:11434";
                var ollamaModel = OllamaModelBox.Text?.Trim() ?? "llama3";

                if (string.IsNullOrEmpty(ollamaEndpoint))
                {
                    _messages.Add(new ChatMessage("system", "⚠️ Ollama endpoint is required."));
                    ScrollToBottom();
                    return;
                }

                _aiService.ConfigureOllama(ollamaEndpoint, ollamaModel);
                providerName = "Ollama (Offline)";
                break;

            default:
                return;
        }

        SaveSettingsToFile(provider);
        IsSettingsVisible = false;
        _messages.Add(new ChatMessage("system", $"✅ Settings saved. Using **{providerName}** as AI provider."));
        ScrollToBottom();
    }

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        IsSettingsVisible = false;
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinButton.Content = Topmost ? "📌 Unpin" : "📌 Pin";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _messages.Clear();
        _aiService.ClearHistory();
        _messages.Add(new ChatMessage("system", "💬 Chat history cleared."));
    }

    private void ChatWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        var focused = Keyboard.FocusedElement as IInputElement;
        if (focused == null) return;

        switch (e.Key)
        {
            case Key.V:
                if (focused is System.Windows.Controls.TextBox || focused is PasswordBox || focused is RichTextBox)
                {
                    if (ApplicationCommands.Paste.CanExecute(null, focused))
                    {
                        ApplicationCommands.Paste.Execute(null, focused);
                        e.Handled = true;
                    }
                }
                break;
            case Key.C:
                if (focused is System.Windows.Controls.TextBox || focused is RichTextBox)
                {
                    if (ApplicationCommands.Copy.CanExecute(null, focused))
                    {
                        ApplicationCommands.Copy.Execute(null, focused);
                        e.Handled = true;
                    }
                }
                break;
            case Key.A:
                if (focused is System.Windows.Controls.TextBox textBox)
                {
                    textBox.SelectAll();
                    e.Handled = true;
                }
                else if (focused is RichTextBox richTextBox)
                {
                    richTextBox.SelectAll();
                    e.Handled = true;
                }
                break;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _aiService.Dispose();
    }

    private void LoadSettings()
    {
        try
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RevAI");

            var configFile = Path.Combine(configDir, "settings.json");

            if (File.Exists(configFile))
            {
                var json = File.ReadAllText(configFile);
                var config = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json);

                if (config != null)
                {
                    // Set provider combo box
                    for (int i = 0; i < ProviderComboBox.Items.Count; i++)
                    {
                        if ((ProviderComboBox.Items[i] as ComboBoxItem)?.Tag?.ToString() == config.Provider.ToString())
                        {
                            ProviderComboBox.SelectedIndex = i;
                            break;
                        }
                    }

                    // Load Copilot settings
                    EndpointBox.Text = config.Endpoint;
                    DeploymentBox.Text = config.DeploymentName;
                    TenantIdBox.Text = config.TenantId;

                    // Load OpenAI settings
                    OpenAiApiKeyBox.Password = config.OpenAiApiKey;
                    OpenAiModelBox.Text = string.IsNullOrEmpty(config.OpenAiModel) ? "gpt-4o" : config.OpenAiModel;

                    // Load Claude settings
                    ClaudeApiKeyBox.Password = config.ClaudeApiKey;
                    ClaudeModelBox.Text = string.IsNullOrEmpty(config.ClaudeModel) ? "claude-sonnet-4-20250514" : config.ClaudeModel;

                    // Load Ollama settings
                    OllamaEndpointBox.Text = string.IsNullOrEmpty(config.OllamaEndpoint) ? "http://127.0.0.1:11434" : config.OllamaEndpoint;
                    OllamaModelBox.Text = string.IsNullOrEmpty(config.OllamaModel) ? "llama3" : config.OllamaModel;

                    // Configure the active provider
                    switch (config.Provider)
                    {
                        case AiProvider.Copilot:
                            if (!string.IsNullOrEmpty(config.Endpoint) && !string.IsNullOrEmpty(config.DeploymentName))
                                _aiService.ConfigureCopilot(config.Endpoint, config.DeploymentName, config.TenantId);
                            break;
                        case AiProvider.OpenAI:
                            if (!string.IsNullOrEmpty(config.OpenAiApiKey))
                                _aiService.ConfigureOpenAi(config.OpenAiApiKey, config.OpenAiModel);
                            break;
                        case AiProvider.Claude:
                            if (!string.IsNullOrEmpty(config.ClaudeApiKey))
                                _aiService.ConfigureClaude(config.ClaudeApiKey, config.ClaudeModel);
                            break;
                        case AiProvider.Ollama:
                            _aiService.ConfigureOllama(config.OllamaEndpoint, config.OllamaModel);
                            break;
                    }
                }
            }
        }
        catch
        {
            // Settings not available yet — that's fine
        }
    }

    private void SaveSettingsToFile(AiProvider provider)
    {
        try
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RevAI");

            Directory.CreateDirectory(configDir);

            var config = new AppConfig
            {
                Provider = provider,
                Endpoint = EndpointBox.Text?.Trim() ?? "",
                DeploymentName = DeploymentBox.Text?.Trim() ?? "",
                TenantId = TenantIdBox.Text?.Trim() ?? "",
                OpenAiApiKey = OpenAiApiKeyBox.Password?.Trim() ?? "",
                OpenAiModel = OpenAiModelBox.Text?.Trim() ?? "gpt-4o",
                ClaudeApiKey = ClaudeApiKeyBox.Password?.Trim() ?? "",
                ClaudeModel = ClaudeModelBox.Text?.Trim() ?? "claude-sonnet-4-20250514",
                OllamaEndpoint = OllamaEndpointBox.Text?.Trim() ?? "http://127.0.0.1:11434",
                OllamaModel = OllamaModelBox.Text?.Trim() ?? "llama3",
            };

            var json = System.Text.Json.JsonSerializer.Serialize(config,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(Path.Combine(configDir, "settings.json"), json);
        }
        catch
        {
            // Silently fail — settings are not critical
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
