using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(Character))]
public class CharacterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default fields for the Character scriptable object
        DrawDefaultInspector();

        // Get the Character object we are currently inspecting
        Character character = (Character)target;

        EditorGUILayout.Space();

        // --- Create Enemy Button Logic ---
        // Only show the button if the "Becomes Enemy" field is empty
        if (character.becomesEnemy == null)
        {
            if (GUILayout.Button("Create Enemy from Character"))
            {
                CreateEnemyFromCharacter(character);
            }
        }
    }

    /// <summary>
    /// Creates a new Enemy asset based on the Character's data.
    /// </summary>
    /// <param name="character">The character to base the enemy on.</param>
    private void CreateEnemyFromCharacter(Character character)
    {
        // 1. Create a new Enemy ScriptableObject instance
        Enemy newEnemy = CreateInstance<Enemy>();

        // 2. Set the enemy's name to match the character's name
        // This also sets the default asset file name.
        newEnemy.name = character.name;

        // You could also copy over other relevant data here, for example:
        // newEnemy.maxHealth = 100; // Default health
        // newEnemy.description = $"A hostile version of {character.name}.";

        // 3. Define the path and create the folder if it doesn't exist
        string directory = "Assets/Resources/Enemies";
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newEnemy.name + ".asset"));

        // 4. Create the asset file on disk
        AssetDatabase.CreateAsset(newEnemy, assetPath);

        // 5. Assign the newly created enemy to the character's "becomesEnemy" field
        character.becomesEnemy = newEnemy;

        // 6. Mark the character asset as "dirty" so Unity knows to save the change
        EditorUtility.SetDirty(character);

        // 7. Save all modified assets to disk and refresh the project view
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created new Enemy '{newEnemy.name}' at '{assetPath}' and linked it to Character '{character.name}'.");
    }
}