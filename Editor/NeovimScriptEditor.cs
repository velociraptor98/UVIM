using System;
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
	/// macOS only. The socket default (/tmp/unity.pipe) and the process handling are not
	/// exercised on other platforms, so rather than ship defaults that are untested, this
	/// simply does not register elsewhere and Unity keeps offering its usual editors.
	///
	/// Project file generation is delegated to the MIT-licensed generator shipped in
	/// com.unity.ide.visualstudio rather than reimplemented: the SDK-style .csproj/.slnx it
	/// writes are exactly what that package produces for VS Code, and Roslyn-based LSPs load
	/// them with the plain .NET SDK — no Mono MSBuild required.
	/// </summary>
	[InitializeOnLoad]
	public class NeovimScriptEditor : IExternalCodeEditor
	{
		// Lazy so the reflection (and any failure logging) runs when generation is first
		// needed, not on every domain reload. Value is null when the generator could not be
		// created; callers degrade — generation is skipped but files still open in Neovim.
		private static readonly Lazy<IGenerator> LazyGenerator = new Lazy<IGenerator>(CreateGenerator);

		internal static IGenerator Generator => LazyGenerator.Value;

		/// <summary>
		/// The public ProjectGeneration base is not usable directly: its GetProjectHeader is a
		/// stub that leaves the header null, so Sync() writes the solution and then throws
		/// NullReferenceException on the first .csproj. The real generators are internal and
		/// normally reached through the selected Visual Studio product's installation — which
		/// does not exist when Neovim is the selected editor. Instantiate the SDK-style one
		/// reflectively; returns null (rather than the broken base) if the internal type ever
		/// moves, so failure is a clear Console error instead of a half-written solution.
		/// </summary>
		private static IGenerator CreateGenerator()
		{
			var type = typeof(ProjectGeneration).Assembly
				.GetType("Microsoft.Unity.VisualStudio.Editor.SdkStyleProjectGeneration");

			if (type != null && Activator.CreateInstance(type) is IGenerator generator)
				return generator;

			Debug.LogError(
				"[Neovim] com.unity.ide.visualstudio no longer exposes SdkStyleProjectGeneration; " +
				"project file generation is disabled. Pin com.unity.ide.visualstudio to a 2.0.x version.");
			return null;
		}

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
			Generator?.Sync();
		}

		public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
		{
			Generator?.SyncIfNeeded(
				addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles),
				importedFiles);
		}

		public bool OpenProject(string path, int line, int column)
		{
			var generator = Generator;
			if (generator != null)
			{
				// path is empty for "Open C# Project" — we still want to open the editor, just with no file.
				if (!string.IsNullOrEmpty(path) && !generator.IsSupportedFile(path))
					return false;

				if (!generator.HasSolutionBeenGenerated())
					generator.Sync();
			}

			return NeovimLauncher.Open(CodeEditor.CurrentEditorInstallation, path, line, column);
		}

		public void OnGUI()
		{
			EditorGUILayout.LabelField("Neovim", EditorStyles.boldLabel);

			NeovimLauncher.ServerPipe = EditorGUILayout.TextField(
				new GUIContent("Server pipe", "Neovim's --listen socket. Files open in whichever Neovim is listening here."),
				NeovimLauncher.ServerPipe);

			EditorGUILayout.HelpBox(
				$"Start Neovim in any terminal with:\n    nvim --listen {NeovimLauncher.ServerPipe}\n" +
				"Files from Unity then open in that session.",
				MessageType.None);

			if (Generator == null)
			{
				EditorGUILayout.HelpBox(
					"Project file generation is unavailable — the installed com.unity.ide.visualstudio " +
					"is incompatible. See the Console for details.",
					MessageType.Error);
				return;
			}

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
			var prevValue = Generator.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
			var newValue = EditorGUILayout.Toggle(label, prevValue);
			if (newValue != prevValue)
				Generator.AssemblyNameProvider.ToggleProjectGeneration(preference);
		}

		private void RegenerateButton()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel("");
			if (GUILayout.Button("Regenerate project files", GUILayout.Width(220)))
				Generator.Sync();
			EditorGUILayout.EndHorizontal();
		}
	}
}
