# Neovim Editor for Unity

Use Neovim as Unity's **External Script Editor** — register it in External Tools, keep
`.csproj`/`.slnx` generated for your LSP, and open files at the right line in the Neovim
you already have running.

Your Neovim runs wherever you like — any terminal, any multiplexer. This package never
spawns one; it only talks to the socket you tell it about, so it has no opinion about your
terminal emulator.

## Why this exists

Unity only regenerates project files when a *recognised* IDE package's editor is selected.
Neovim isn't recognised, so the usual workaround is to leave Visual Studio or Rider selected
and never open it. This package registers Neovim properly, so Unity treats it as a first-class
editor.

Project generation is **not** reimplemented here. This package calls the MIT-licensed
SDK-style generator that ships in `com.unity.ide.visualstudio`, so the `.csproj`/`.slnx`
files your LSP reads are exactly the ones that package produces for VS Code — and they stay
correct as Unity changes. SDK-style projects load in Roslyn-based LSPs with the plain
.NET SDK; no Mono MSBuild is needed.

## Requirements

- **macOS.** The socket default and process handling are not exercised on Windows or Linux, so
  rather than ship untested defaults, this package does not register itself there at all — Unity
  keeps offering its usual editors and nothing changes.
- Unity 2019.4 or newer
- `com.unity.ide.visualstudio` 2.0.26+ (ships with Unity by default; declared as a dependency)
- Neovim on your `PATH`, or at a well-known location

## Install

Package Manager → **Add package from git URL…**

```
https://github.com/velociraptor98/UVIM.git
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
a distinct path and start that project's Neovim with the matching `--listen`. The setting is
stored per project (in `UserSettings/`), so each project keeps its own value.

Files are sent to Neovim in the background; your terminal is not raised. Bind a hotkey in your
window manager or terminal to switch to it, whatever you already use.

## Neovim setup

Any Roslyn-based C# LSP works. The steps below get completion working with
[roslyn.nvim](https://github.com/seblyng/roslyn.nvim) driving Microsoft's Roslyn language
server — the same server VS Code uses.

### 1. Install the .NET SDK and the language server

The server runs on the .NET SDK:

```sh
brew install --cask dotnet-sdk
```

Then install the server itself. With [mason.nvim](https://github.com/mason-org/mason.nvim)
it is in the default registry:

```
:MasonInstall roslyn-language-server
```

### 2. Install roslyn.nvim

```lua
{
  "seblyng/roslyn.nvim",
  ft = "cs",
  opts = {
    broad_search = true,      -- Unity projects often have several solutions
    filewatching = "roslyn",  -- more stable on large projects
    -- Prefer the .slnx this package generates over any leftover legacy .sln
    -- from a previous Rider/Visual Studio setup. Returning nil falls back to
    -- the plugin's normal target prompt.
    choose_target = function(targets)
      local slnx = vim.tbl_filter(function(t)
        return t:match("%.slnx$") ~= nil
      end, targets)
      if #slnx == 1 then
        return slnx[1]
      end
    end,
  },
}
```

**LazyVim users:** also disable the C# servers LazyVim would otherwise auto-start, or they
fight roslyn.nvim over `cs` buffers (omnisharp used to claim them outright; `roslyn_ls`
attaches a second, duplicate client):

```lua
{
  "neovim/nvim-lspconfig",
  opts = {
    servers = {
      omnisharp = { enabled = false },
      roslyn_ls = { enabled = false },
    },
  },
}
```

### 3. Optional server settings

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

### 4. Generate project files and open the project

In Unity, with Neovim selected as the external editor: **Preferences → External Tools →
Regenerate project files** (afterwards Unity keeps them in sync on script changes). You
should see a `.slnx` and one `.csproj` per assembly in the project root.

Then start Neovim in the project root and open any script:

```sh
cd /path/to/your-unity-project
nvim --listen /tmp/unity.pipe Assets/Scripts/Player.cs
```

Give the server a minute on first open (see below), then completion, go-to-definition, and
diagnostics work on Unity types.

### macOS: Roslyn needs Mono's reference assemblies (legacy projects only)

Projects generated by **this package** are SDK-style and need none of this. But projects
last generated by the Rider or Visual Studio packages are legacy .NET Framework style:
Roslyn's build host wants Mono MSBuild for those; Homebrew's `mono` ships without it, so
Roslyn falls back to the .NET SDK — which then cannot resolve framework references
(`netstandard`, `System.*`) and completion silently returns nothing for every Unity type.
Until such projects are regenerated, point reference resolution at Mono's bundled reference
assemblies (`brew install mono` provides them):

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

### If completion stays empty

- `:checkhealth vim.lsp` / `:LspInfo` — is a client named `roslyn` attached?
- `:Roslyn target` — if several solutions were found, make sure the `.slnx` is selected.
- No `.csproj` files next to the solution means generation never ran — regenerate from
  Unity's Preferences and check the Unity Console for errors.
- The server's log is at `~/.local/state/nvim/lsp.log`; "unresolved dependencies" warnings
  for a handful of package projects are normal, per-project load errors are not.

## Not included

- **Debugging.** Use [nvim-dap-unity](https://github.com/ownself/nvim-dap-unity); Unity's
  editor runs Mono, so `netcoredbg` will not attach.
- **IL2CPP debugging.** Not supported by any Neovim adapter.

## Licence

MIT. Project generation is delegated to `com.unity.ide.visualstudio`
(© Unity Technologies, © Microsoft Corporation, MIT).
