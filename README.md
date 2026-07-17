# Neovim Editor for Unity

Use Neovim as Unity's **External Script Editor** — register it in External Tools, keep
`.csproj`/`.sln` generated for your LSP, and open files at the right line in the Neovim
you already have running.

## Why this exists

Unity only regenerates project files when a *recognised* IDE package's editor is selected.
Neovim isn't recognised, so the usual workaround is to leave Visual Studio or Rider selected
and never open it. This package registers Neovim properly, so Unity treats it as a first-class
editor.

Project generation is **not** reimplemented here. This package references the MIT-licensed
generator that ships in `com.unity.ide.visualstudio` and calls it directly, so the `.csproj`
files your LSP reads are byte-for-byte the ones Visual Studio would produce — and they stay
correct as Unity changes.

## Requirements

- **macOS.** Spawning and focusing a terminal has no portable form, so rather than ship untested
  defaults for Windows and Linux, this package does not register itself there at all — Unity keeps
  offering its usual editors and nothing changes.
- Unity 2019.4 or newer
- `com.unity.ide.visualstudio` (ships with Unity by default; declared as a dependency)
- Neovim on your `PATH`, or at a well-known location

## Install

Package Manager → **Add package from git URL…**

```
https://github.com/<you>/com.ikistudio.ide.uvim.git
```

Then **Edit → Preferences → External Tools → External Script Editor → Neovim**.

## How opening a file works

Two paths, tried in order:

1. **A Neovim is already listening on the socket** (`/tmp/unity.pipe` by default) — the file is
   sent to that instance with `--remote-silent`, the cursor is moved, and the terminal is
   focused. Your session, buffers, and LSP stay warm.
2. **Nothing is listening** — the configured terminal command runs and spawns one.

To get path 1, start Neovim with the socket:

```sh
nvim --listen /tmp/unity.pipe
```

Or always listen, from your Neovim config:

```lua
vim.api.nvim_create_autocmd("VimEnter", {
  callback = function()
    local pipe = "/tmp/unity.pipe"
    if vim.fn.filereadable(pipe) == 0 then
      pcall(vim.fn.serverstart, pipe)
    end
  end,
})
```

## Settings

In **Preferences → External Tools**, with Neovim selected:

| Setting | Meaning |
|---|---|
| **Server pipe** | Socket to look for a running Neovim on. Default `/tmp/unity.pipe`. |
| **Terminal command** | Run when nothing is listening. Placeholders: `{nvim}` `{pipe}` `{file}` `{line}` `{column}` `{project}`. |
| **Focus command** | Run to raise the terminal after a file is sent to an already-running Neovim. Default `open -a Ghostty`. Empty means never focus. |
| **Generate .csproj files for** | Which package types get project files. Same flags as the Visual Studio package (they share storage). |
| **Regenerate project files** | Force a full re-sync. |

Default terminal command:

```
open -na Ghostty --args --working-directory={project} -e {nvim} --listen {pipe} +{line} {file}
```

Swap `Ghostty` for `kitty`, `WezTerm`, `Alacritty`, or whatever you use — in the **Focus command**
too, which is a separate setting. What each part buys:

| Part | Drop it and… |
|---|---|
| `open -na Ghostty --args` | Nothing launches. Ghostty does not support CLI launching on macOS; this is the documented form. |
| `--working-directory={project}` | Neovim opens in `/` instead of the project root, so LSP root detection can't find your `.sln`. |
| `-e` | Ghostty opens a plain shell instead of running Neovim. |
| `--listen {pipe}` | Every click from Unity spawns a **new** window instead of reusing this one. |
| `+{line}` | You land at the top of the file instead of on the error. |

Note `--listen` only matters on cold start — once a Neovim is listening, files are sent over the
socket and this command is never run.

Both commands run through the shell, so `;`, loops, and quoting all work.

### cmux

[cmux](https://cmux.com) is driven by a CLI rather than `open --args`:

Set **Terminal command** to (as one line):

```sh
open -a cmux; for i in 1 2 3 4 5 6 7 8 9 10; do /Applications/cmux.app/Contents/Resources/bin/cmux ping >/dev/null 2>&1 && break; sleep 0.2; done; /Applications/cmux.app/Contents/Resources/bin/cmux workspace create --focus true --cwd {project} --command '{nvim} --listen {pipe} +{line} {file}'
```

and **Focus command** to `open -a cmux`.

Four things that are easy to get wrong:

- **`cmux` must be an absolute path.** This is the one that bites hardest, because it half-works:
  cmux opens and then nothing loads. Unity launched from Finder or the Dock inherits a minimal
  `PATH` — cmux is not in `/usr/local/bin`, it lives inside the app bundle and is only put on
  `PATH` by the shell integration in terminals cmux itself spawns. So `open` (which is
  `/usr/bin/open`) succeeds while the following `cmux` is not found. A login shell does **not**
  fix this; nothing in your shell rc puts cmux on `PATH` either.
- **`--command` must be wrapped in single quotes.** It takes a shell string that cmux types into
  the workspace, not an argv list like Ghostty's `-e`. The placeholders expand to double-quoted
  values, so double-quoting `--command` too collapses the nesting and any path containing a space
  splits into two arguments.
- **`--focus true` is required.** It defaults to `false`, which builds the workspace behind
  whatever you are looking at.
- **`open -a cmux` and the `ping` loop are not decoration.** `cmux workspace create` talks to a
  Unix socket and fails with `Socket not found` if cmux is not already running — unlike `open -na
  Ghostty`, it will not start the app for you. Since the terminal command only ever runs when no
  Neovim is listening, this is exactly the cold-start case. `open -a` launches cmux (or does
  nothing if it is up) and the bounded loop waits for the socket without spinning forever.

`workspace create` was called `new-workspace` in older cmux; the old name still works but prints a
deprecation hint.

## Neovim side

Any C# LSP works. With [roslyn.nvim](https://github.com/seblyng/roslyn.nvim):

```lua
{
  "seblyng/roslyn.nvim",
  ft = "cs",
  opts = {
    broad_search = true,      -- Unity projects often have several solutions
    filewatching = "roslyn",  -- more stable on large projects
  },
}
```

Server settings go through `vim.lsp.config`, **not** the plugin's `opts`:

```lua
vim.lsp.config("roslyn", {
  settings = {
    ["csharp|background_analysis"] = {
      dotnet_compiler_diagnostics_scope = "fullSolution",
      dotnet_analyzer_diagnostics_scope = "fullSolution",
    },
  },
})
```

## Not included

- **Debugging.** Use [nvim-dap-unity](https://github.com/ownself/nvim-dap-unity); Unity's
  editor runs Mono, so `netcoredbg` will not attach.
- **IL2CPP debugging.** Not supported by any Neovim adapter.

## Licence

MIT. Project generation is delegated to `com.unity.ide.visualstudio`
(© Unity Technologies, © Microsoft Corporation, MIT).
