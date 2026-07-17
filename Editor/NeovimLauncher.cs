using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IkiStudio.Ide.Uvim
{
	/// <summary>
	/// Opens files in Neovim. macOS only — see <see cref="NeovimScriptEditor"/>.
	///
	/// Neovim is a terminal program, so "launching" it means one of two things:
	///   1. A Neovim is already listening on a socket — send the file to it (fast, keeps your session).
	///   2. Nothing is listening — run the configured terminal command to spawn one.
	/// </summary>
	internal static class NeovimLauncher
	{
		private const string PipePrefKey = "ikistudio_ide_uvim_pipe";
		private const string TerminalPrefKey = "ikistudio_ide_uvim_terminal";
		private const string FocusPrefKey = "ikistudio_ide_uvim_focus";

		public const string DefaultServerPipe = "/tmp/unity.pipe";

		// `+{line}` rather than `+call cursor({line},{column})`: same jump, but no spaces, so the
		// argument survives `open --args` and the shell without needing to be quoted.
		public const string DefaultTerminalCommand =
			"open -na Ghostty --args --working-directory={project} -e {nvim} --listen {pipe} +{line} {file}";

		// Run after sending a file to an already-running Neovim, to raise its terminal.
		public const string DefaultFocusCommand = "open -a Ghostty";

		public static string ServerPipe
		{
			get => EditorPrefs.GetString(PipePrefKey, DefaultServerPipe);
			set => EditorPrefs.SetString(PipePrefKey, value);
		}

		public static string TerminalCommand
		{
			get => EditorPrefs.GetString(TerminalPrefKey, DefaultTerminalCommand);
			set => EditorPrefs.SetString(TerminalPrefKey, value);
		}

		public static string FocusCommand
		{
			get => EditorPrefs.GetString(FocusPrefKey, DefaultFocusCommand);
			set => EditorPrefs.SetString(FocusPrefKey, value);
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

			var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
			file = string.IsNullOrEmpty(file) ? "" : Path.GetFullPath(file);

			return TrySendToRunningInstance(nvimPath, file, line, column)
				|| TrySpawnTerminal(nvimPath, file, line, column, projectRoot);
		}

		/// <summary>
		/// nvim --server PIPE --remote-* only succeeds if something is actually listening.
		/// The socket exists as a file, so File.Exists is a cheap way to check first.
		/// </summary>
		private static bool TrySendToRunningInstance(string nvimPath, string file, int line, int column)
		{
			var pipe = ServerPipe;
			if (string.IsNullOrEmpty(pipe))
				return false;

			if (!File.Exists(pipe))
				return false;

			if (string.IsNullOrEmpty(file))
				return Run(nvimPath, $"--server {Quote(pipe)} --remote-send {Quote("<C-\\><C-N>")}");

			// --remote-silent opens the file without erroring if it is already open.
			if (!Run(nvimPath, $"--server {Quote(pipe)} --remote-silent {Quote(file)}"))
				return false;

			// Then place the cursor. <C-\><C-N> first, so this still works from insert/terminal mode.
			var keys = $"<C-\\><C-N>:call cursor({line},{column})<CR>zz";
			Run(nvimPath, $"--server {Quote(pipe)} --remote-send {Quote(keys)}");

			FocusTerminal();
			return true;
		}

		private static bool TrySpawnTerminal(string nvimPath, string file, int line, int column, string projectRoot)
		{
			var command = TerminalCommand;
			if (string.IsNullOrEmpty(command))
			{
				Debug.LogWarning("[Neovim] No terminal command configured. Set one in Edit > Preferences > External Tools.");
				return false;
			}

			command = command
				.Replace("{nvim}", Quote(nvimPath))
				.Replace("{pipe}", Quote(ServerPipe))
				.Replace("{file}", string.IsNullOrEmpty(file) ? "" : Quote(file))
				.Replace("{line}", line.ToString())
				.Replace("{column}", column.ToString())
				.Replace("{project}", Quote(projectRoot));

			return RunShell(command);
		}

		/// <summary>
		/// Sending keys to a background Neovim does not raise its window — bring the terminal forward.
		/// </summary>
		private static void FocusTerminal()
		{
			var command = FocusCommand;
			if (string.IsNullOrEmpty(command))
				return;

			RunShell(command);
		}

		/// <summary>
		/// Hand a command line to the shell, so the configured commands can use shell syntax
		/// (quoting, `;`, loops) rather than being limited to a single executable.
		/// </summary>
		private static bool RunShell(string command)
		{
			return Run("/bin/sh", $"-c {Quote(command)}");
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
