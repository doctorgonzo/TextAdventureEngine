using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// Editor window for viewing and toggling **all** worldFlags in the GameController at runtime,
/// including those set to false. Supports filtering, refresh, save, and clear.
/// Optionally reads a public "definedFlags" array on GameController for a master list of flags.
/// </summary>
public class FlagInspectorWindow : EditorWindow
{
    private Vector2 scrollPos;
    private string filter = string.Empty;
    private GameController controller;
    private List<string> allFlags = new List<string>();

    [MenuItem("Window/Flag Inspector")]
    public static void ShowWindow() => GetWindow<FlagInspectorWindow>("Flag Inspector");

    private void OnEnable()
    {
        RefreshFlags();
    }

    /// <summary>
    /// Reloads the worldFlags dictionary and captures all keys, even false ones,
    /// sourcing from a definedFlags array on the GameController if present.
    /// </summary>
    private void RefreshFlags()
    {
        controller = UnityEngine.Object.FindObjectOfType<GameController>();
        if (controller == null)
        {
            allFlags.Clear();
            return;
        }

        // Load the master registry of flags from Resources/GlobalFlagRegistry
        var registry = Resources.Load<FlagRegistry>("GlobalFlagRegistry");
        if (registry != null && registry.flags != null && registry.flags.Count > 0)
        {
            allFlags = registry.flags.OrderBy(k => k).ToList();
        }
        else
        {
            // Fallback to runtime flags in the worldFlags dictionary
            var flagsDict = GetWorldFlags();
            allFlags = flagsDict != null
                ? flagsDict.Keys.OrderBy(k => k).ToList()
                : new List<string>();
        }
    }

    /// <summary>
    /// Resolves the controller's worldFlags dictionary via reflection. Handles
    /// worldFlags being exposed either as a private field or (since the
    /// WorldState refactor) as a private forwarding property.
    /// </summary>
    private Dictionary<string, bool> GetWorldFlags()
    {
        if (controller == null) return null;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        object value =
            typeof(GameController).GetProperty("worldFlags", flags)?.GetValue(controller)
            ?? typeof(GameController).GetField("worldFlags", flags)?.GetValue(controller);
        return value as Dictionary<string, bool>;
    }

    private void OnGUI()
    {
        GUILayout.Label("World Flags Inspector", EditorStyles.boldLabel);

        if (controller == null)
        {
            EditorGUILayout.HelpBox("No GameController found in the scene.", MessageType.Warning);
            if (GUILayout.Button("Refresh")) RefreshFlags();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        filter = EditorGUILayout.TextField("Filter", filter);
        if (GUILayout.Button("Clear", GUILayout.Width(50))) filter = string.Empty;
        if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshFlags();
        if (GUILayout.Button("Save Flags", GUILayout.Width(80)))
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.EndHorizontal();

        var flagsDict = GetWorldFlags();
        if (flagsDict == null)
        {
            EditorGUILayout.HelpBox("worldFlags dictionary not found.", MessageType.Error);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (var key in allFlags)
        {
            if (!string.IsNullOrEmpty(filter) &&
                key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            bool currentValue = flagsDict.ContainsKey(key) ? flagsDict[key] : false;
            bool newValue = EditorGUILayout.ToggleLeft(key, currentValue);
            if (newValue != currentValue)
            {
                flagsDict[key] = newValue;
                EditorUtility.SetDirty(controller);
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
