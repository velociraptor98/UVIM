# Changelog

## [0.1.1] - 2026-07-19

### Fixed
- Project generation crashed with NullReferenceException after writing the solution, leaving
  no `.csproj` files at all: the public `ProjectGeneration` base emits a null project header —
  the real generators are internal and only reachable through a Visual Studio installation,
  which never exists when Neovim is the selected editor. Generation now goes through the
  package's SDK-style generator (obtained reflectively). This also means generated projects
  (`.slnx` + SDK-style `.csproj`) load in Roslyn LSPs with the plain .NET SDK — no Mono
  MSBuild needed.

### Added
- Project files regenerate automatically on the first load with a new Unity editor version —
  previously an upgrade left every `.csproj` pointing at the old editor's DLLs and the LSP
  silently lost all Unity types until a manual regeneration.
- **Test connection** and **Copy launch command** buttons plus a socket-status line in
  Preferences. The test round-trips through `--remote-expr`, so it catches a stale socket
  file left by a crashed Neovim, which a plain file check cannot.
- The default server pipe is per project (`/tmp/unity-<project>-<hash>.pipe`), so several
  Unity projects work side by side with no configuration.
- **Assets → Regenerate Project Files** menu item (enabled while Neovim is the selected
  editor); same action as the Preferences button, and bindable to a shortcut.
- `ProjectFileSync.SyncAll`, a batch-mode `-executeMethod` entry point so project files can be
  regenerated headlessly after a Unity editor upgrade.
- README: step-by-step Neovim setup guide (SDK + language server install, roslyn.nvim spec
  with `.slnx` target selection, LazyVim conflict note, troubleshooting), macOS section on
  `FrameworkPathOverride` for legacy projects (Homebrew mono has no MSBuild, which silently
  breaks all framework references in Roslyn), headless regeneration, and first-load latency.

- The server pipe setting is now stored per project (EditorUserSettings) instead of per user
  (EditorPrefs), so multi-project setups can actually have one socket per project as the README
  describes. Existing EditorPrefs values are read as a fallback.
- Opening a file waits for the remote open to be delivered before moving the cursor;
  previously the two commands raced and the cursor could land in the previously focused buffer.
- Remote commands now check the nvim exit code, so a stale socket (left by a crashed Neovim)
  produces a Console warning instead of silently doing nothing.
- Shell discovery of nvim waited for output before checking its timeout, so a hanging login
  shell could freeze the editor UI indefinitely; the timeout now actually applies.
- Unity's "no specific line" signal (-1) is respected: the session's cursor position is kept
  instead of jumping to line 1.

### Changed
- Installation discovery is cached; previously every query from the Preferences window spawned
  a login shell.
- The generator is created lazily (first use, not every domain reload), and if the internal
  generator type is ever unavailable the plugin disables generation with a clear Console error
  instead of falling back to the broken base generator.
- `com.unity.ide.visualstudio` dependency floor raised to 2.0.26, the version the reflection
  into `SdkStyleProjectGeneration` is tested against.

## [0.1.0] - 2026-07-17

### Added
- Register Neovim as an External Script Editor via `IExternalCodeEditor`. macOS only — the package
  does not register itself on Windows or Linux.
- `.csproj`/`.sln` generation, delegated to the generator in `com.unity.ide.visualstudio`.
- Open files at line/column in a running Neovim over `--server`/`--remote-silent`. Neovim must be
  started by you with `--listen <pipe>`; the package never spawns it, so it works with any terminal
  emulator or multiplexer. A Console warning names the socket when nothing is listening.
- Preferences UI: server pipe, project generation flags, regenerate button.
- Discovery of `nvim` via login-shell `command -v` plus well-known paths.
