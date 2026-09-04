# Project Guidelines & Agent Context: FileFormatAIStudio

## GitHub & Project Management
- **Repository**: `OpenizePtyLtd/fileformat-studio`
- **GitHub CLI**: `gh` is installed and available system-wide on `PATH` across all directories (fallback path: `"C:\Program Files\GitHub CLI\gh.exe"` if a running daemon has an unrefreshed environment).
- **Issue Tracking Conventions**:
  - Keep Epic/Feature issues open as the parent tracking issue until all subtasks are finished.
  - Granular tasks should follow conventional commits:
    - `feat(security): ...`
    - `feat(data): ...`
    - `feat(ui): ...`
    - `fix(...): ...`
  - Reference related issues in commits and PRs (e.g. `Resolves #10`, `Part of #10`).

## Architecture & Technology Stack
- **Framework**: WinUI 3 / Windows App SDK on .NET 10 (`net10.0-windows10.0.19041.0`).
- **Architecture**: MVVM with `CommunityToolkit.Mvvm`, EF Core with SQLite (`fileformat_studio.db`).
- **Security**: Sensitive secrets (like LLM API keys) must be protected using Windows DPAPI (`DataProtectionService` via `ProtectedData.Protect` / `Unprotect`) and masked in XAML using `PasswordBox`.

