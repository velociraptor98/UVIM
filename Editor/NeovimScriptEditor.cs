using System.Linq;
using Microsoft.Unity.VisualStudio.Editor;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace IkiStudio.Ide.Uvim
{
	/// <summary>
	/// Registers Neovim as an External Script Editor.
	///
	/// macOS only. Spawning and focusing a terminal has no portable form — every part of it
	/// (`open -na`, `/bin/sh`, the socket in /tmp) is platform-specific — so rather than ship
	/// defaults for platforms that are not exercised, this simply does not register elsewhere
	/// and Unity keeps offering its usual editors.
	///
	/// Project file generation is delegated to the MIT-licensed generator shipped in
	/// com.unity.ide.visualstudio rather than reimplemented, so the .csproj/.sln files
	/// Neovim's LSP consumes are byte-for-byte the ones Visual Studio would get.
	/// </summary>
	[InitializeOnLoad]
	public class NeovimScriptEditor : IExternalCodeEditor
	{
		private readonly IGenerator _generator = new ProjectGeneration();

		static NeovimScriptEditor()
		{
			if (Application.platform != RuntimePlatform.OSXEditor)
				return;

			CodeEditor.Register(new NeovimScriptEditor());
		}

		public CodeEditor.Installation[] Installations => Discovery.GetInstallations();

		public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
		{
			return Discovery.TryGetInstallationForPath(editorPath, out installation);
		}

		public void Initialize(string editorInstallationPath)
		{
		}

		public void SyncAll()
		{
			_generator.Sync();
		}

		public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
		{
			_generator.SyncIfNeeded(
				addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles),
				importedFiles);
		}

		public bool OpenProject(string path, int line, int column)
		{
			// path is empty for "Open C# Project" — we still want to open the editor, just with no file.
			if (!string.IsNullOrEmpty(path) && !_generator.IsSupportedFile(path))
				return false;

			if (!_generator.HasSolutionBeenGenerated())
				_generator.Sync();

			return NeovimLauncher.Open(CodeEditor.CurrentEditorInstallation, path, line, column);
		}

		public void OnGUI()
		{
			EditorGUILayout.LabelField("Neovim", EditorStyles.boldLabel);

			NeovimLauncher.ServerPipe = EditorGUILayout.TextField(
				new GUIContent("Server pipe", "Neovim's --listen socket. If a Neovim is already listening here, files open in it instead of spawning a new window."),
				NeovimLauncher.ServerPipe);

			EditorGUILayout.LabelField(new GUIContent("Terminal command"), EditorStyles.miniBoldLabel);
			EditorGUILayout.HelpBox(
				"Run when no Neovim is listening on the pipe.\n" +
				"Placeholders: {nvim} {pipe} {file} {line} {column} {project}",
				MessageType.None);
			NeovimLauncher.TerminalCommand = EditorGUILayout.TextArea(
				NeovimLauncher.TerminalCommand, GUILayout.Height(38));

			if (GUILayout.Button("Reset terminal command to default"))
				NeovimLauncher.TerminalCommand = NeovimLauncher.DefaultTerminalCommand;

			EditorGUILayout.LabelField(new GUIContent("Focus command"), EditorStyles.miniBoldLabel);
			EditorGUILayout.HelpBox(
				"Run to raise the terminal after sending a file to a running Neovim. " +
				"Leave empty to never focus.",
				MessageType.None);
			NeovimLauncher.FocusCommand = EditorGUILayout.TextArea(
				NeovimLauncher.FocusCommand, GUILayout.Height(19));

			if (GUILayout.Button("Reset focus command to default"))
				NeovimLauncher.FocusCommand = NeovimLauncher.DefaultFocusCommand;

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Generate .csproj files for:");
			EditorGUI.indentLevel++;
			SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages");
			SettingsButton(ProjectGenerationFlag.Local, "Local packages");
			SettingsButton(ProjectGenerationFlag.Registry, "Registry packages");
			SettingsButton(ProjectGenerationFlag.Git, "Git packages");
			SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages");
			SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball");
			SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources");
			SettingsButton(ProjectGenerationFlag.PlayerAssemblies, "Player projects");
			RegenerateButton();
			EditorGUI.indentLevel--;
		}

		private void SettingsButton(ProjectGenerationFlag preference, string label)
		{
			var prevValue = _generator.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
			var newValue = EditorGUILayout.Toggle(label, prevValue);
			if (newValue != prevValue)
				_generator.AssemblyNameProvider.ToggleProjectGeneration(preference);
		}

		private void RegenerateButton()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel("");
			if (GUILayout.Button("Regenerate project files", GUILayout.Width(220)))
				_generator.Sync();
			EditorGUILayout.EndHorizontal();
		}
	}
}
