namespace TextEngine.EditorTools
{
    using System.Linq;
    using TextEngine;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Where the editor tools create new content assets. The project's
    /// EngineSettings asset decides (its 'Content Root Folder' field), so a
    /// customer's game content doesn't have to share the demo's folders.
    /// Falls back to the demo content root when no EngineSettings exists.
    /// </summary>
    public static class EditorPaths
    {
        public const string DefaultContentRoot = "Assets/TextAdventureEngine/Demo/Resources";

        public static string ContentRoot
        {
            get
            {
                var settings = AssetDatabase.FindAssets("t:EngineSettings")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<EngineSettings>(AssetDatabase.GUIDToAssetPath(guid)))
                    .FirstOrDefault(s => s != null);
                if (settings != null && !string.IsNullOrEmpty(settings.contentRootFolder))
                {
                    string root = settings.contentRootFolder.TrimEnd('/');
                    if (!root.Contains("/Resources"))
                    {
                        Debug.LogWarning($"[Text Engine] Content Root Folder '{root}' (on '{settings.name}') is not inside a Resources folder — the engine's catalogs will not load assets created there.");
                    }
                    return root;
                }
                return DefaultContentRoot;
            }
        }

        public static string Folder(string subfolder) => ContentRoot + "/" + subfolder;
    }
}
