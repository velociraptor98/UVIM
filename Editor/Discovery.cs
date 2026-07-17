using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.CodeEditor;

namespace Kunal.Ide.Neovim
{
	/// <summary>
	/// Locates nvim binaries so Unity can list them in the External Tools dropdown.
	/// </summary>
	internal static class Discovery
	{
		private static readonly string[] WellKnownPaths =
		{
			// macOS (Apple Silicon homebrew, Intel homebrew, MacPorts)
			"/opt/homebrew/bin/nvim",
			"/usr/local/bin/nvim",
			"/opt/local/bin/nvim",
			// Linux
			"/usr/bin/nvim",
			"/usr/local/bin/nvim",
			"/snap/bin/nvim",
			"/var/lib/flatpak/exports/bin/io.neovim.nvim",
			"/home/linuxbrew/.linuxbrew/bin/nvim",
			// Windows
			@"C:\Program Files\Neovim\bin\nvim.exe",
			@"C:\tools\neovim\Neovim\bin\nvim.exe",
		};

		public static CodeEditor.Installation[] GetInstallations()
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
			var isWindows = Path.DirectorySeparatorChar == '\\';
			var file = isWindows ? "where" : "/bin/sh";
			// A login shell so PATH matches what the user's terminal sees (homebrew, mise, asdf...).
			var args = isWindows ? "nvim" : "-lc \"command -v nvim\"";

			try
			{
				using (var process = new Process())
				{
					process.StartInfo = new ProcessStartInfo(file, args)
					{
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true,
					};
					process.Start();
					var output = process.StandardOutput.ReadToEnd();

					// Guard against a shell that never returns (bad rc file, prompt waiting on input).
					if (!process.WaitForExit(3000))
					{
						try { process.Kill(); } catch { /* already gone */ }
						return null;
					}

					return output;
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
