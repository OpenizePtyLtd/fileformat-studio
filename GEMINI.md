# Project Guidelines & Agent Context: FileFormatAIStudio

## Repository & Issue Management
- **Repository**: `OpenizePtyLtd/fileformat-studio`
- **Issue Tracking**: Managed in company **Redmine instance** (switched from GitHub Issues; historical tasks 1-65 remain mapped in `docs/master_tasks.md`).
- **Redmine Configuration**:
  - Connection credentials (`REDMINE_URL`, `REDMINE_API_KEY`, `REDMINE_PROJECT_ID`, `REDMINE_ASSIGN_TO_ID`, `REDMINE_USER`, `REDMINE_CATEGORY_ID`) must always be read dynamically from the untracked `.env` file in the repository root.
  - Never hardcode or commit Redmine URLs or API keys to the repository.
  - When creating tasks/issues in Redmine via API:
    - Assign them to `REDMINE_ASSIGN_TO_ID` (or `23`).
    - Set `category_id` to `REDMINE_CATEGORY_ID` (or `261`).
    - **Never prefix** the subject with `[fileformat-studio]` or `[TASK-XX]`. Use clean titles directly (e.g. `feat(...): ...`).
- **Issue & Commit Conventions**:
  - Granular tasks should follow conventional commits referencing Redmine issues:
    - `feat(...): ... (refs #XXXXX)`
    - `fix(...): ... (fixes #XXXXX)`
    - `docs(...): ...`
  - Reference related issues in PRs and commit logs.

## Architecture & Technology Stack
- **Framework**: WinUI 3 / Windows App SDK on .NET 10 (`net10.0-windows10.0.19041.0`).
- **Architecture**: MVVM with `CommunityToolkit.Mvvm`, EF Core with SQLite (`fileformat_studio.db`).
- **Security**: Sensitive secrets (like LLM API keys) must be protected using Windows DPAPI (`DataProtectionService` via `ProtectedData.Protect` / `Unprotect`) and masked in XAML using `PasswordBox`.

