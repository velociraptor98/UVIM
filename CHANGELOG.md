# Changelog

## [0.1.0] - 2026-07-17

### Added
- Register Neovim as an External Script Editor via `IExternalCodeEditor`. macOS only — the package
  does not register itself on Windows or Linux.
- `.csproj`/`.sln` generation, delegated to the generator in `com.unity.ide.visualstudio`.
- Open files at line/column in a running Neovim over `--server`/`--remote-silent`,
  falling back to spawning a configurable terminal.
- Preferences UI: server pipe, terminal command, focus command, project generation flags,
  regenerate button.
- Discovery of `nvim` via login-shell `command -v` plus well-known paths.
- Configurable focus command, replacing the hardcoded `open -na <app>` parsing of the terminal
  command. Terminals that are not launched that way — cmux, or anything CLI-driven — can now be
  focused, and setting it empty disables focusing entirely.
- Documented settings for using [cmux](https://cmux.com) as the terminal.
