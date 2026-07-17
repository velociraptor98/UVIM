# Changelog

## [0.1.0] - 2026-07-17

### Added
- Register Neovim as an External Script Editor via `IExternalCodeEditor`.
- `.csproj`/`.sln` generation, delegated to the generator in `com.unity.ide.visualstudio`.
- Open files at line/column in a running Neovim over `--server`/`--remote-silent`,
  falling back to spawning a configurable terminal.
- Preferences UI: server pipe, terminal command, project generation flags, regenerate button.
- Discovery of `nvim` via login-shell `command -v` plus well-known paths.
