using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace IkiStudio.Ide.Uvim
{
	/// <summary>
	/// Opens files in a Neovim that is already listening on a socket. macOS only —
	/// see <see cref="NeovimScriptEditor"/>.
	///
	/// Spawning Neovim would mean picking a terminal emulator and a way to focus it, neither of
	/// which has a form that works everywhere. So this only ever talks to a Neovim the user
	/// started themselves with `nvim --listen /tmp/unity.pipe`, whatever it is running inside.
	/// </summary>
	internal static class NeovimLauncher
	{
		private const string PipePrefKey = "ikistudio_ide_uvim_pipe";

		public const string DefaultServerPipe = "/tmp/unity.pipe";

		public static string ServerPipe
		{
			get => EditorPrefs.GetString(PipePrefKey, DefaultServerPipe);
			set => EditorPrefs.SetString(PipePrefKey, value);
		}

		public static bool Open(string nvimPath, string file, int line, int column)
		{
			if (string.IsNullOrEmpty(nvimPath))
			{
				Debug.LogWarning("[Neovim] No Neovim executable selected. Set one in Edit > Preferences > External Tools.");
				return false;
			}

			// Unity passes -1 when it has no specific location (e.g. "Open C# Project").
			line = Math.Max(line, 1);
			column = Math.Max(column, 1);

			file = string.IsNullOrEmpty(file) ? "" : Path.GetFullPath(file);

			return TrySendToRunningInstance(nvimPath, file, line, column);
		}

		/// <summary>
		/// nvim --server PIPE --remote-* only succeeds if something is actually listening.
		/// The socket exists as a file, so File.Exists is a cheap way to check first.
		/// </summary>
		private static bool TrySendToRunningInstance(string nvimPath, string file, int line, int column)
		{
			var pipe = ServerPipe;
			if (string.IsNullOrEmpty(pipe))
			{
				Debug.LogWarning("[Neovim] No server pipe configured. Set one in Edit > Preferences > External Tools.");
				return false;
			}

			if (!File.Exists(pipe))
			{
				Debug.LogWarning(
					$"[Neovim] No Neovim listening on '{pipe}'. Start one with: nvim --listen {pipe}");
				return false;
			}

			if (string.IsNullOrEmpty(file))
				return Run(nvimPath, $"--server {Quote(pipe)} --remote-send {Quote("<C-\\><C-N>")}");

			// --remote-silent opens the file without erroring if it is already open.
			if (!Run(nvimPath, $"--server {Quote(pipe)} --remote-silent {Quote(file)}"))
				return false;

			// Then place the cursor. <C-\><C-N> first, so this still works from insert/terminal mode.
			var keys = $"<C-\\><C-N>:call cursor({line},{column})<CR>zz";
			Run(nvimPath, $"--server {Quote(pipe)} --remote-send {Quote(keys)}");

			return true;
		}

		private static bool Run(string file, string args)
		{
			try
			{
				using (var process = new Process())
				{
					process.StartInfo = new ProcessStartInfo(file, args)
					{
						UseShellExecute = false,
						CreateNoWindow = true,
					};
					return process.Start();
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[Neovim] Failed to run '{file} {args}': {e.Message}");
				return false;
			}
		}

		private static string Quote(string value)
		{
			if (string.IsNullOrEmpty(value))
				return "\"\"";
			return value.IndexOf('"') >= 0
				? "\"" + value.Replace("\"", "\\\"") + "\""
				: "\"" + value + "\"";
		}
	}
}
