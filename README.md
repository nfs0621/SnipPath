# SnipPath

Windows Snip-style capture tool that saves the image to `%TEMP%\SnipPath` and copies the file path to the clipboard.
Snips older than 7 days are deleted automatically.
Useful when working with agentic CLI like Codex, Gemini, or Claude Code.

Default hotkeys:
- Snip and add path to clipboard: Ctrl+Shift+S
- Snip and add image to clipboard: Win+Shift+C
- Quit: Ctrl+Shift+Q

Tray icon includes Snip and Quit actions.

## Download and install

- Download the latest portable ZIP from the GitHub Releases page.
- Unzip anywhere (no installer required).
- Run `SnipPath.exe`.

Notes:
- This portable build is framework-dependent. Install the .NET 8 Desktop Runtime if Windows prompts you.

## Build and run

```powershell
dotnet build
dotnet run
```
