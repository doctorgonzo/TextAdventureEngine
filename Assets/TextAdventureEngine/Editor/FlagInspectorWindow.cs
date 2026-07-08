namespace TextEngine.EditorTools
{
    using TextEngine;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Runtime debugging window: view and toggle every world flag while the
    /// game is playing. The list merges the FlagRegistry master list with
    /// whatever flags the running game has actually set, and repaints live so
    /// flags flipped by gameplay show up as they happen.
    /// </summary>
    public class FlagInspectorWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private string filter = string.Empty;
        private GameController controller;

        [MenuItem("Window/Flag Inspector")]
        public static void ShowWindow() => GetWindow<FlagInspectorWindow>("Flag Inspector");

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            FindController();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            FindController();
            Repaint();
        }

        // Called ~10x/sec while the window is visible — keeps the display in
        // sync with flags the running game sets.
        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        private void FindController()
        {
            controller = UnityEngine.Object.FindFirstObjectByType<GameController>();
        }

        // Registry flags + any runtime-set flags the registry doesn't know about.
        private List<string> GatherFlagNames()
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var registry = Resources.Load<FlagRegistry>("GlobalFlagRegistry");
            if (registry != null && registry.flags != null)
            {
                foreach (var flag in registry.flags)
                {
                    if (!string.IsNullOrEmpty(flag)) names.Add(flag);
                }
            }
            if (controller != null)
            {
                foreach (var flag in controller.RuntimeFlagNames) names.Add(flag);
            }
            return names.ToList();
        }

        private void OnGUI()
        {
            GUILayout.Label("World Flags Inspector", EditorStyles.boldLabel);

            if (controller == null) FindController();
            if (!EditorApplication.isPlaying || controller == null)
            {
                EditorGUILayout.HelpBox("Enter Play Mode with a GameController in the scene to inspect and toggle flags.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            filter = EditorGUILayout.TextField("Filter", filter);
            if (GUILayout.Button("Clear", GUILayout.Width(50))) filter = string.Empty;
            EditorGUILayout.EndHorizontal();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var key in GatherFlagNames())
            {
                if (!string.IsNullOrEmpty(filter) &&
                    key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                bool currentValue = controller.GetWorldFlag(key);
                bool newValue = EditorGUILayout.ToggleLeft(key, currentValue);
                if (newValue != currentValue)
                {
                    controller.SetWorldFlag(key, newValue);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
