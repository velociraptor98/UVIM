# Neovim Editor for Unity

Use Neovim as Unity's **External Script Editor** — register it in External Tools, keep
`.csproj`/`.sln` generated for your LSP, and open files at the right line in the Neovim
you already have running.

Your Neovim runs wherever you like — any terminal, any multiplexer. This package never
spawns one; it only talks to the socket you tell it about, so it has no opinion about your
terminal emulator.

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

- **macOS.** The socket default and process handling are not exercised on Windows or Linux, so
  rather than ship untested defaults, this package does not register itself there at all — Unity
  keeps offering its usual editors and nothing changes.
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

You start Neovim listening on a socket (`/tmp/unity.pipe` by default). When you open a file from
Unity, it is sent to that instance with `--remote-silent` and the cursor is moved to the line.
Your session, buffers, and LSP stay warm.

If nothing is listening on the socket, nothing opens and a Console warning tells you how to start
one. This package will not spawn Neovim for you — that would mean choosing a terminal emulator on
your behalf, and this way any terminal, multiplexer, or remote session works the same.

Start Neovim with the socket:

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
| **Generate .csproj files for** | Which package types get project files. Same flags as the Visual Studio package (they share storage). |
| **Regenerate project files** | Force a full re-sync. |

Project files go stale when the Unity editor version changes — every `.csproj` points at DLLs
under `/Applications/Unity/Hub/Editor/<old version>/` and the LSP loses all Unity types until
they are regenerated. After an upgrade, either click **Regenerate project files** or run it
headless (with the project closed):

```sh
"/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit -projectPath <project> \
  -executeMethod IkiStudio.Ide.Uvim.ProjectFileSync.SyncAll
```

If you run several Unity projects at once, give each one its own socket — point **Server pipe** at
a distinct path and start that project's Neovim with the matching `--listen`.

Files are sent to Neovim in the background; your terminal is not raised. Bind a hotkey in your
window manager or terminal to switch to it, whatever you already use.

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

### macOS: Roslyn needs Mono's reference assemblies

Unity generates legacy .NET Framework projects. Roslyn's build host wants Mono MSBuild for
those; Homebrew's `mono` ships without it, so Roslyn falls back to the .NET SDK — which then
cannot resolve framework references (`netstandard`, `System.*`) and completion silently
returns nothing for every Unity type. Point reference resolution at Mono's bundled reference
assemblies instead (`brew install mono` provides them):

```lua
local cmd_env
local mono = vim.fn.exepath("mono")
if mono ~= "" then
  local api_dir = vim.fs.normalize(vim.fn.resolve(mono) .. "/../../lib/mono/4.7.1-api")
  if vim.uv.fs_stat(api_dir) then
    cmd_env = { FrameworkPathOverride = api_dir }
  end
end
vim.lsp.config("roslyn", { cmd_env = cmd_env })
```

The `Mono MSBuild could not be found` warning in `~/.local/state/nvim/lsp.log` still prints —
that is expected; the override fixes resolution, not discovery.

### First load is slow

Roslyn loads the entire solution before it can answer anything. On a Unity project this
takes a minute or two after the first `.cs` buffer opens; completion is empty until then.
This is once per session — subsequent files are instant.

## Not included

- **Debugging.** Use [nvim-dap-unity](https://github.com/ownself/nvim-dap-unity); Unity's
  editor runs Mono, so `netcoredbg` will not attach.
- **IL2CPP debugging.** Not supported by any Neovim adapter.

## Licence

MIT. Project generation is delegated to `com.unity.ide.visualstudio`
(© Unity Technologies, © Microsoft Corporation, MIT).
