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
- `ProjectFileSync.SyncAll`, a batch-mode `-executeMethod` entry point so project files can be
  regenerated headlessly after a Unity editor upgrade.
- README: macOS section on `FrameworkPathOverride` (Homebrew mono has no MSBuild, which
  silently breaks all framework references in Roslyn), headless regeneration, and first-load
  latency.

### Changed
- Installation discovery is cached; previously every query from the Preferences window spawned
  a login shell.

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
