namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic;

    [CreateAssetMenu(menuName = "Text Adventure/Scenario", order = 13)]
    public class TextEngineScenario : ScriptableObject
    {
        [Tooltip("Display name for the scenario")] public string scenarioName;

        [Tooltip("Name of the starting location to load")] public string startingLocation;

        [Tooltip("Items the player begins with")] public List<Item> startingInventory = new List<Item>();

        [Tooltip("Enemies to spawn in the starting location")] public List<Enemy> enemiesInLocation = new List<Enemy>();

        [Tooltip("Reset player health to max on load?")] public bool resetPlayerHealth = true;
    }
}
