using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Kunal.Ide.Neovim
{
	/// <summary>
	/// Opens files in Neovim.
	///
	/// Neovim is a terminal program, so "launching" it means one of two things:
	///   1. A Neovim is already listening on a socket — send the file to it (fast, keeps your session).
	///   2. Nothing is listening — run the configured terminal command to spawn one.
	/// </summary>
	internal static class NeovimLauncher
	{
		private const string PipePrefKey = "kunal_ide_neovim_pipe";
		private const string TerminalPrefKey = "kunal_ide_neovim_terminal";

		public static string DefaultServerPipe =>
			IsWindows ? @"\\.\pipe\unity.pipe" : "/tmp/unity.pipe";

		// `+{line}` rather than `+call cursor({line},{column})`: same jump, but no spaces, so the
		// argument survives `open --args` and the shell without needing to be quoted.
		public static string DefaultTerminalCommand
		{
			get
			{
				if (IsMacOS)
					return "open -na Ghostty --args --working-directory={project} -e {nvim} --listen {pipe} +{line} {file}";
				if (IsWindows)
					return "wt.exe -d {project} {nvim} --listen {pipe} +{line} {file}";
				return "ghostty --working-directory={project} -e {nvim} --listen {pipe} +{line} {file}";
			}
		}

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

		private static bool IsWindows => Application.platform == RuntimePlatform.WindowsEditor;
		private static bool IsMacOS => Application.platform == RuntimePlatform.OSXEditor;

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
		/// On unix the socket exists as a file; on Windows named pipes are not visible to File.Exists.
		/// </summary>
		private static bool TrySendToRunningInstance(string nvimPath, string file, int line, int column)
		{
			var pipe = ServerPipe;
			if (string.IsNullOrEmpty(pipe))
				return false;

			if (!IsWindows && !File.Exists(pipe))
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

			if (IsWindows)
				return Run("cmd.exe", $"/c {command}");

			return Run("/bin/sh", $"-c {Quote(command)}");
		}

		/// <summary>
		/// Sending keys to a background Neovim does not raise its window — bring the terminal forward.
		/// </summary>
		private static void FocusTerminal()
		{
			if (!IsMacOS)
				return;

			// Derive the app from the configured terminal command rather than hardcoding Ghostty.
			var command = TerminalCommand ?? "";
			var marker = "open -na ";
			var index = command.IndexOf(marker, StringComparison.Ordinal);
			if (index < 0)
				return;

			var rest = command.Substring(index + marker.Length);
			var app = rest.Split(' ')[0];
			if (!string.IsNullOrEmpty(app))
				Run("open", $"-a {Quote(app)}");
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
