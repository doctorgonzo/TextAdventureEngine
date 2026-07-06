using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// A streamlined Editor window for loading TextEngine scenarios in Play Mode,
/// with an automatic "space" press shortly after load to advance UI.
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
    /// Loads all TextEngineScenario assets from Resources/Scenarios folder.
    /// </summary>
    private void RefreshScenarioList()
    {
        scenarios = Resources.LoadAll<TextEngineScenario>("Scenarios");
        scenarioNames = scenarios.Select(s => s.scenarioName).ToArray();
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

        // Load button
        if (GUILayout.Button("Load Selected Scenario"))
        {
            LoadSelectedScenario();
        }

        // Optional: Refresh list
        if (GUILayout.Button("Refresh List")) RefreshScenarioList();
    }

    /// <summary>
    /// Invokes GameController.LoadScenario on the selected SO,
    /// skipping the typewriter effect and auto-pressing space shortly after.
    /// </summary>
    private void LoadSelectedScenario()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode to load scenarios.");
            return;
        }

        var controller = Object.FindObjectOfType<GameController>();
        if (controller == null)
        {
            Debug.LogError("No GameController found in the scene.");
            return;
        }

        var scenario = scenarios[selectedIndex];

        // Load the scenario data (sets currentLocation, inventory, enemies, health)
        controller.LoadScenario(scenario);
        // Rebuild world/UI and display description instantly
        controller.DisplayLocation(useTypewriter: false);

        Debug.Log($"Loaded scenario: {scenario.scenarioName} (instant display)");

        // After a short delay, simulate a space-bar press to advance any UI
        Task.Delay(250).ContinueWith(_ => {
            EditorApplication.delayCall += () => {
                var ctrl = Object.FindObjectOfType<GameController>();
                if (ctrl != null)
                {
                    // Simulate pressing space to advance text or close dialogs
                    ctrl.ParsePlayerCommand(" ");
                    Debug.Log("Auto-pressed space after scenario load");
                }
            };
        });
    }
}
