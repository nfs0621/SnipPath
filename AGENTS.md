# Repository Guidelines

## Project Structure & Module Organization
- Source: WPF app in the repo root (e.g., `App.xaml`, `HotkeyWindow.cs`, `SnipWindow.xaml`).
- Assets: `assets/` for app resources such as the icon (`assets/snippath.ico`).
- Build outputs: `bin/` and `obj/` (generated; do not edit).

## Build, Test, and Development Commands
- `dotnet build SnipPath.csproj` builds the application.
- `dotnet run --project SnipPath.csproj` runs the app locally.
- `dotnet clean SnipPath.csproj` removes build outputs.

## Coding Style & Naming Conventions
- Language: C# (.NET 8, WPF).
- Indentation: 4 spaces.
- Types/Methods: PascalCase; private fields: `_camelCase`.
- Prefer explicit namespaces when WPF and WinForms types overlap (e.g., `System.Windows.Application` vs `System.Windows.Forms.Application`).

## Testing Guidelines
- No test project is configured in this repo.
- If adding tests, use a `tests/` folder and name projects like `SnipPath.Tests`.
- Prefer `dotnet test` as the standard runner.

## Commit & Pull Request Guidelines
- No git history was found; use concise, imperative commit messages (e.g., "Fix snip window close guard").
- PRs should include:
  - A short summary of behavior changes.
  - Steps to verify (commands + manual steps).
  - Screenshots or short clips if UI behavior changes.

## Security & Configuration Tips
- This app uses global hotkeys; be careful when changing shortcuts to avoid conflicts.
- The snip window closes on deactivation; keep focus/activation logic stable to avoid crashes.
