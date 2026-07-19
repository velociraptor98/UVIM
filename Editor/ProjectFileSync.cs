using Unity.CodeEditor;
using UnityEditor;

namespace IkiStudio.Ide.Uvim
{
	/// <summary>
	/// Batch-mode entry point for project file generation, so stale .csproj/.sln files (the
	/// usual aftermath of a Unity editor upgrade) can be regenerated from a shell without
	/// opening the editor UI:
	///
	///   Unity -batchmode -quit -projectPath &lt;project&gt; -executeMethod IkiStudio.Ide.Uvim.ProjectFileSync.SyncAll
	///
	/// Fails if the project is already open in an editor — Unity holds a lock per project.
	/// Also exposes the same action as an Assets menu item.
	/// </summary>
	public static class ProjectFileSync
	{
		private const string MenuPath = "Assets/Regenerate Project Files";

		[MenuItem(MenuPath)]
		private static void RegenerateFromMenu()
		{
			SyncAll();
		}

		// Only meaningful while Neovim owns generation; Rider/Visual Studio manage their own
		// project files and regenerating over them would just cause churn.
		[MenuItem(MenuPath, true)]
		private static bool RegenerateFromMenuValidation()
		{
			return CodeEditor.Editor.CurrentCodeEditor is NeovimScriptEditor;
		}

		public static void SyncAll()
		{
			var generator = NeovimScriptEditor.Generator;
			if (generator == null)
			{
				// Throw rather than return so batch mode exits non-zero; the specific cause was
				// already logged by NeovimScriptEditor.
				throw new System.InvalidOperationException(
					"Project file generation is unavailable — the installed com.unity.ide.visualstudio is incompatible.");
			}

			generator.Sync();
		}
	}
}
