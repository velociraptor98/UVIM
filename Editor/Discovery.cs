using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.CodeEditor;

namespace IkiStudio.Ide.Uvim
{
	/// <summary>
	/// Locates nvim binaries so Unity can list them in the External Tools dropdown.
	/// macOS only — see <see cref="NeovimScriptEditor"/>.
	/// </summary>
	internal static class Discovery
	{
		// Apple Silicon homebrew, Intel homebrew, MacPorts.
		private static readonly string[] WellKnownPaths =
		{
			"/opt/homebrew/bin/nvim",
			"/usr/local/bin/nvim",
			"/opt/local/bin/nvim",
		};

		private static CodeEditor.Installation[] _cache;

		public static CodeEditor.Installation[] GetInstallations()
		{
			// Unity queries installations while drawing the Preferences window; without a
			// cache every repaint spawns a login shell (RunWhich) and stalls the UI. Cached
			// until the next domain reload — an nvim installed mid-session shows up after the
			// next script recompile.
			if (_cache == null)
				_cache = FindInstallations();
			return _cache;
		}

		private static CodeEditor.Installation[] FindInstallations()
		{
			var found = new List<CodeEditor.Installation>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var path in WhichNvim().Concat(WellKnownPaths))
			{
				if (string.IsNullOrEmpty(path) || !File.Exists(path))
					continue;

				var resolved = Path.GetFullPath(path);
				if (!seen.Add(resolved))
					continue;

				found.Add(new CodeEditor.Installation
				{
					Name = $"Neovim ({resolved})",
					Path = resolved,
				});
			}

			return found.ToArray();
		}

		public static bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
		{
			installation = default;

			if (string.IsNullOrEmpty(editorPath) || !IsNvim(editorPath))
				return false;

			installation = new CodeEditor.Installation
			{
				Name = $"Neovim ({editorPath})",
				Path = editorPath,
			};
			return true;
		}

		private static bool IsNvim(string editorPath)
		{
			var name = Path.GetFileNameWithoutExtension(editorPath);
			return string.Equals(name, "nvim", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(name, "neovim", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Ask the shell where nvim lives, so PATH-only installs still show up in the dropdown.
		/// </summary>
		private static IEnumerable<string> WhichNvim()
		{
			var output = RunWhich();
			if (string.IsNullOrEmpty(output))
				return Enumerable.Empty<string>();

			return output
				.Split('\n')
				.Select(line => line.Trim())
				.Where(line => line.Length > 0);
		}

		private static string RunWhich()
		{
			// A login shell so PATH matches what the user's terminal sees (homebrew, mise, asdf...).
			try
			{
				using (var process = new Process())
				{
					// stderr is deliberately not redirected: rc files can be chatty, and an unread
					// redirected pipe that fills up would deadlock the wait below.
					process.StartInfo = new ProcessStartInfo("/bin/sh", "-lc \"command -v nvim\"")
					{
						UseShellExecute = false,
						RedirectStandardOutput = true,
						CreateNoWindow = true,
					};
					process.Start();

					// Wait BEFORE reading: ReadToEnd blocks until stdout closes, so a shell that
					// hangs (bad rc file, prompt waiting on input) would freeze the editor and a
					// timeout placed after the read could never fire. The output is one short
					// line, so it cannot fill the pipe buffer and stall the shell either.
					if (!process.WaitForExit(3000))
					{
						try { process.Kill(); } catch { /* already gone */ }
						return null;
					}

					return process.StandardOutput.ReadToEnd();
				}
			}
			catch
			{
				// No shell, or it refused to run — fall back to the well-known paths.
				return null;
			}
		}
	}
}
