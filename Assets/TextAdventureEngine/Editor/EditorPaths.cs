namespace TextEngine.EditorTools
{
    /// <summary>
    /// Where the editor tools create new content assets. Centralized so the
    /// location is defined exactly once; the Asset Store roadmap's Phase 3
    /// makes this configurable per project (via EngineSettings), at which
    /// point only this class needs to read that setting.
    /// </summary>
    public static class EditorPaths
    {
        public const string ContentRoot = "Assets/TextAdventureEngine/Demo/Resources";

        public static string Folder(string subfolder) => ContentRoot + "/" + subfolder;
    }
}
