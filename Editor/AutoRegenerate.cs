using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace IkiStudio.Ide.Uvim
{
	/// <summary>
	/// Regenerates project files when the Unity editor version changes. Generated .csproj
	/// files reference DLLs under the installed editor's path
	/// (/Applications/Unity/Hub/Editor/&lt;version&gt;/...), so after an upgrade every reference is
	/// a dead path and the LSP silently loses all Unity types. Regenerating on the first load
	/// with a new editor removes that failure mode entirely.
	/// </summary>
	[InitializeOnLoad]
	internal static class AutoRegenerate
	{
		private const string VersionKey = "ikistudio_ide_uvim_generated_with";

		static AutoRegenerate()
		{
			if (Application.platform != RuntimePlatform.OSXEditor)
				return;

			// In batch mode generation is driven explicitly (ProjectFileSync); running here too
			// would sync twice.
			if (Application.isBatchMode)
				return;

			// Delayed: during InitializeOnLoad the CodeEditor registry and asset database are
			// not reliably initialized yet.
			EditorApplication.delayCall += SyncIfEditorVersionChanged;
		}

		private static void SyncIfEditorVersionChanged()
		{
			if (!(CodeEditor.Editor.CurrentCodeEditor is NeovimScriptEditor))
				return;

			var generator = NeovimScriptEditor.Generator;
			if (generator == null)
				return;

			var current = Application.unityVersion;

			// Nothing generated yet means nothing stale to fix — just record the version so a
			// later editor upgrade is detected relative to the first real generation.
			if (!generator.HasSolutionBeenGenerated())
			{
				EditorUserSettings.SetConfigValue(VersionKey, current);
				return;
			}

			var last = EditorUserSettings.GetConfigValue(VersionKey);
			if (last == current)
				return;

			Debug.Log($"[Neovim] Unity editor version changed ({last ?? "unknown"} → {current}); regenerating project files.");
			generator.Sync();
			EditorUserSettings.SetConfigValue(VersionKey, current);
		}
	}
}
