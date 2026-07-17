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

- Unity 2019.4 or newer
- `com.unity.ide.visualstudio` (ships with Unity by default; declared as a dependency)
- Neovim on your `PATH`, or at a well-known location

## Install

Package Manager → **Add package from git URL…**

```
https://github.com/<you>/com.kunal.ide.neovim.git
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
    local pipe = vim.fn.has("win32") == 1 and [[\\.\pipe\unity.pipe]] or "/tmp/unity.pipe"
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
| **Server pipe** | Socket to look for a running Neovim on. Default `/tmp/unity.pipe` (`\\.\pipe\unity.pipe` on Windows). |
| **Terminal command** | Run when nothing is listening. Placeholders: `{nvim}` `{pipe}` `{file}` `{line}` `{column}` `{project}`. |
| **Generate .csproj files for** | Which package types get project files. Same flags as the Visual Studio package (they share storage). |
| **Regenerate project files** | Force a full re-sync. |

Default terminal command on macOS:

```
open -na Ghostty --args --working-directory={project} -e {nvim} --listen {pipe} "+call cursor({line},{column})" {file}
```

Swap `Ghostty` for `kitty`, `WezTerm`, `Alacritty`, or whatever you use.

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
