namespace TextEngine.EditorTools
{
    using TextEngine;

    using UnityEditor;
    using UnityEngine;
    using System.Linq;

    /// <summary>
    /// A streamlined Editor window for loading TextEngine scenarios in Play
    /// Mode. Displays the scenario instantly (no typewriter).
    /// </summary>
    public class ScenarioLoaderWindow : EditorWindow
    {
        private TextEngineScenario[] scenarios = new TextEngineScenario[0];
        private string[] scenarioNames = new string[0];
        private int selectedIndex = -1;

        [MenuItem("Window/Scenario Loader")]
        public static void ShowWindow()
        {
            GetWindow<ScenarioLoaderWindow>("Scenario Loader");
        }

        private void OnEnable()
        {
            RefreshScenarioList();
        }

        /// <summary>
        /// Loads all TextEngineScenario assets from Resources/Scenarios folders.
        /// </summary>
        private void RefreshScenarioList()
        {
            scenarios = Resources.LoadAll<TextEngineScenario>("Scenarios");
            // Fall back to the asset name when the display name is blank so the
            // dropdown never shows empty entries.
            scenarioNames = scenarios
                .Select(s => string.IsNullOrEmpty(s.scenarioName) ? s.name : s.scenarioName)
                .ToArray();
            selectedIndex = scenarios.Length > 0 ? 0 : -1;
        }

        private void OnGUI()
        {
            GUILayout.Label("Scenario Loader", EditorStyles.boldLabel);

            if (scenarioNames.Length == 0)
            {
                EditorGUILayout.HelpBox("No scenarios found in Resources/Scenarios.", MessageType.Info);
                if (GUILayout.Button("Refresh")) RefreshScenarioList();
                return;
            }

            // Scenario dropdown
            selectedIndex = EditorGUILayout.Popup("Scenario", selectedIndex, scenarioNames);

            // Load button — only usable in Play Mode.
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Load Selected Scenario"))
                {
                    LoadSelectedScenario();
                }
            }
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to load scenarios.", MessageType.Info);
            }

            if (GUILayout.Button("Refresh List")) RefreshScenarioList();
        }

        /// <summary>
        /// Invokes GameController.LoadScenario on the selected scenario and
        /// finishes any streaming text so the result is visible immediately.
        /// </summary>
        private void LoadSelectedScenario()
        {
            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null)
            {
                Debug.LogError("[Text Engine] No GameController found in the scene.");
                return;
            }

            var scenario = scenarios[selectedIndex];
            // LoadScenario rebuilds the world state and displays the location.
            controller.LoadScenario(scenario);
            controller.SkipTypewriter();
            Debug.Log($"[Text Engine] Loaded scenario: {scenario.scenarioName}");
        }
    }
}
