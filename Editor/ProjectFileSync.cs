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
	/// </summary>
	public static class ProjectFileSync
	{
		public static void SyncAll()
		{
			NeovimScriptEditor.CreateGenerator().Sync();
		}
	}
}
