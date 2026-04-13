# RevAI & RevCode — AI-Powered Revit 2025 Automation Platform

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Revit 2025](https://img.shields.io/badge/Revit-2025-blue)](https://www.autodesk.com/products/revit/)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)

A dual-plugin solution for **Autodesk Revit 2025** that brings AI-powered code generation and a built-in C# code editor directly into the Revit environment. Automate BIM tasks using natural language or write code against the Revit API — all without leaving Revit.

---

## ✨ Features

### RevAI — AI Assistant

- **Natural language to code** — describe what you want in plain English and let AI generate the Revit API code
- **Multiple AI providers** — supports GitHub Copilot (M365 SSO), OpenAI ChatGPT, Anthropic Claude, and Ollama (local/offline)
- **Auto-execution** — generated code runs immediately inside Revit
- **Auto-error recovery** — if code fails, RevAI asks the AI to fix it automatically
- **Conversation context** — follow-up prompts understand previous messages
- **Rich chat UI** — styled message bubbles, code blocks, bold text, and copy support

### RevCode — Code Editor

- **Built-in C# editor** — write and run Revit API code directly
- **Immediate execution** — press **F5** to compile and run
- **Line numbers & output panel** — track your position and see results in real time
- **Flexible entry points** — use `GeneratedCommand.Execute`, any `Execute(UIApplication)`, or `Run(UIApplication)` method
- **Code templates** — start from a ready-made skeleton

### Shared

- Both plugins live under one **"Code & Automations"** ribbon tab
- Runtime C# compilation via **Roslyn** (Microsoft.CodeAnalysis)
- Thread-safe execution through Revit's `ExternalEvent` mechanism
- Pin-to-top windows for multitasking
- No admin privileges required for installation

---

## 📐 Architecture

```
Tab: "Code & Automations"
├── Panel: "AI Assistant"
│   └── Button: "RevAI"   → Chat Window
└── Panel: "Code Editor"
    └── Button: "RevCode"  → Code Editor Window
```

**Execution pipeline (both plugins):**

```
User Input (chat message or editor code)
    ↓
Code String (AI-generated or hand-written)
    ↓
ExternalEvent → marshals to Revit main thread
    ↓
CodeCompiler.CompileAndExecute()
    ├─ Wraps code with default usings (Autodesk.Revit.DB, System.Linq, …)
    ├─ Compiles to IL via Roslyn
    └─ Invokes GeneratedCommand.Execute(UIApplication)
    ↓
Result String → displayed in Chat or Output Panel
```

---

## 🗂️ Project Structure

```
copilot_revit/
├── RevAI.sln                      # Visual Studio solution (2 projects)
├── Install-RevAI.ps1              # No-admin installer / uninstaller
├── LICENSE                        # MIT License
│
└── src/
    ├── RevAI/                     # AI assistant plugin
    │   ├── App.cs                 # Revit IExternalApplication entry point
    │   ├── RevAI.csproj           # .NET 8.0, Roslyn, Azure.Identity
    │   ├── RevAI.addin            # Revit add-in manifest
    │   ├── Commands/              # ShowChatCommand
    │   ├── Core/                  # RevitCodeExecutionHandler
    │   ├── Models/                # ChatMessage, AppConfig
    │   ├── Services/              # AiService (multi-provider), CodeCompiler
    │   ├── UI/                    # ChatWindow (WPF), Converters
    │   └── Resources/             # Icons
    │
    └── RevCode/                   # Code editor plugin
        ├── App.cs                 # Revit IExternalApplication entry point
        ├── RevCode.csproj         # .NET 8.0, Roslyn
        ├── RevCode.addin          # Revit add-in manifest
        ├── Commands/              # ShowEditorCommand
        ├── Core/                  # CodeExecutionHandler
        ├── Services/              # CodeCompiler (flexible entry points)
        ├── UI/                    # CodeEditorWindow (WPF)
        └── Resources/             # Icons
```

---

## 🚀 Getting Started

### Prerequisites

| Requirement | Version |
|---|---|
| [Autodesk Revit](https://www.autodesk.com/products/revit/) | 2025 |
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ |
| [Visual Studio](https://visualstudio.microsoft.com/) (optional) | 2022 17.0+ |

### Build

```bash
dotnet build RevAI.sln -c Release
```

### Install

Run the included PowerShell installer (no admin privileges needed):

```powershell
# Right-click Install-RevAI.ps1 → "Run with PowerShell"
# or:
powershell -ExecutionPolicy Bypass -File Install-RevAI.ps1
```

The installer copies the built DLLs and `.addin` manifests into:

```
%APPDATA%\Autodesk\Revit\Addins\2025\
```

### Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File Install-RevAI.ps1 -Uninstall
```

---

## ⚙️ AI Provider Configuration

After launching Revit and opening the **RevAI** chat window, click the **Settings** gear to configure your preferred AI provider:

| Provider | Auth Method | Notes |
|---|---|---|
| **GitHub Copilot** | M365 SSO (browser sign-in) | Requires an active Copilot subscription |
| **OpenAI ChatGPT** | API key | Uses the OpenAI chat completions API |
| **Anthropic Claude** | API key | Uses the Anthropic messages API |
| **Ollama** | None (local) | Runs entirely offline via a local Ollama server |

Settings are stored per-user at `%APPDATA%\RevAI\settings.json`.

---

## 🎹 Keyboard Shortcuts

| Shortcut | Context | Action |
|---|---|---|
| **Enter** | RevAI chat | Send message |
| **Shift + Enter** | RevAI chat | New line |
| **F5** | RevCode editor | Run code |
| **Ctrl + C / V / A** | Both | Copy / Paste / Select All |

---

## 🤝 Contributing

Contributions are welcome! Feel free to open issues or submit pull requests.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes (`git commit -m "Add my feature"`)
4. Push to the branch (`git push origin feature/my-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

## 👤 Author

**Irfan Irwanuddin**
