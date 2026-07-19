using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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

		private static string _defaultServerPipe;

		/// <summary>
		/// Per-project default, so several Unity projects each talk to their own Neovim with no
		/// configuration: /tmp/unity-&lt;project&gt;-&lt;hash&gt;.pipe. The name keeps it recognizable
		/// in `ls /tmp`; the hash disambiguates projects that share a folder name.
		/// </summary>
		public static string DefaultServerPipe
		{
			get
			{
				if (_defaultServerPipe == null)
				{
					var root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
					_defaultServerPipe = $"/tmp/unity-{SanitizeName(Path.GetFileName(root))}-{(Fnv1a(root) & 0xffff):x4}.pipe";
				}
				return _defaultServerPipe;
			}
		}

		private static string SanitizeName(string name)
		{
			var builder = new StringBuilder(name.Length);
			foreach (var c in name.ToLowerInvariant())
				builder.Append(char.IsLetterOrDigit(c) ? c : '-');
			return builder.ToString();
		}

		private static uint Fnv1a(string text)
		{
			// string.GetHashCode is not guaranteed stable across runs; the socket name must be.
			var hash = 2166136261u;
			foreach (var c in text)
				hash = (hash ^ c) * 16777619u;
			return hash;
		}

		/// <summary>
		/// Stored per project (EditorUserSettings), not per user: running several Unity projects
		/// side by side needs one socket per project, and EditorPrefs would silently share a
		/// single value between them. Reads any value previously stored in EditorPrefs as a
		/// fallback so existing setups migrate on first save.
		/// </summary>
		public static string ServerPipe
		{
			get
			{
				var value = EditorUserSettings.GetConfigValue(PipePrefKey);
				if (!string.IsNullOrEmpty(value))
					return value;
				return EditorPrefs.GetString(PipePrefKey, DefaultServerPipe);
			}
			set => EditorUserSettings.SetConfigValue(PipePrefKey, value);
		}

		/// <summary>
		/// True liveness check: --remote-expr round-trips to the server, so unlike a
		/// File.Exists probe it fails on a socket file that survived a crash with nothing
		/// listening behind it. Returns a human-readable status for the Preferences UI.
		/// </summary>
		public static string TestConnection(string nvimPath)
		{
			var pipe = ServerPipe;

			if (string.IsNullOrEmpty(nvimPath))
				return "No Neovim executable selected.";

			if (!File.Exists(pipe))
				return $"No socket at '{pipe}'. Start Neovim with: nvim --listen {pipe}";

			return Run(nvimPath, $"--server {Quote(pipe)} --remote-expr 1")
				? "Connected — Neovim is listening."
				: "Socket exists but nothing answered. Remove the stale socket and restart Neovim.";
		}

		public static bool Open(string nvimPath, string file, int line, int column)
		{
			if (string.IsNullOrEmpty(nvimPath))
			{
				Debug.LogWarning("[Neovim] No Neovim executable selected. Set one in Edit > Preferences > External Tools.");
				return false;
			}

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

			// --remote-silent opens the file without erroring if it is already open. Run waits
			// for the command to be delivered, so the cursor command below cannot race ahead of
			// the open and land in whichever buffer was focused before.
			if (!Run(nvimPath, $"--server {Quote(pipe)} --remote-silent {Quote(file)}"))
				return false;

			// Unity passes -1 when it has no specific location (e.g. "Open C# Project" or an
			// asset double-click); keep the session's own cursor position in that case.
			if (line > 0)
			{
				column = Math.Max(column, 1);
				// <C-\><C-N> first, so this still works from insert/terminal mode.
				var keys = $"<C-\\><C-N>:call cursor({line},{column})<CR>zz";
				Run(nvimPath, $"--server {Quote(pipe)} --remote-send {Quote(keys)}");
			}

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
					process.Start();

					// `nvim --server` exits as soon as the command is delivered. Waiting here both
					// orders successive commands and surfaces a stale socket: after a Neovim crash
					// the socket file survives with nothing listening, and the remote call is the
					// only thing that can tell — via a non-zero exit code.
					if (!process.WaitForExit(2000))
					{
						try { process.Kill(); } catch { /* already gone */ }
						Debug.LogWarning($"[Neovim] '{file} {args}' did not complete; is '{ServerPipe}' responding?");
						return false;
					}

					if (process.ExitCode != 0)
					{
						Debug.LogWarning(
							$"[Neovim] No Neovim answered on '{ServerPipe}'. If the socket is stale " +
							$"(left over from a crash), remove it and start Neovim with: nvim --listen {ServerPipe}");
						return false;
					}

					return true;
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
