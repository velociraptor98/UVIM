# Changelog

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
