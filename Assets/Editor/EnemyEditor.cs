using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(Enemy))]
public class EnemyEditor : Editor
{
    // --- Properties from Enemy.cs ---
    SerializedProperty enemyName;
    SerializedProperty description;
    SerializedProperty detailedDescription;
    SerializedProperty maxHealth;
    SerializedProperty baseAttack;
    SerializedProperty damageVariance;
    SerializedProperty evasionChance;
    SerializedProperty xpReward;
    SerializedProperty behavior;
    SerializedProperty lootDrops;
    SerializedProperty exitDirectionToReveal;
    SerializedProperty attacksOnSight;

    void OnEnable()
    {
        // Link all the properties
        enemyName = serializedObject.FindProperty("enemyName");
        description = serializedObject.FindProperty("description");
        detailedDescription = serializedObject.FindProperty("detailedDescription");
        maxHealth = serializedObject.FindProperty("maxHealth");
        baseAttack = serializedObject.FindProperty("baseAttack");
        damageVariance = serializedObject.FindProperty("damageVariance");
        evasionChance = serializedObject.FindProperty("evasionChance");
        xpReward = serializedObject.FindProperty("xpReward");
        behavior = serializedObject.FindProperty("behavior");
        lootDrops = serializedObject.FindProperty("lootDrops");
        exitDirectionToReveal = serializedObject.FindProperty("exitDirectionToReveal");
        attacksOnSight = serializedObject.FindProperty("attacksOnSight");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        // --- Draw Core & Combat Stats ---
        EditorGUILayout.PropertyField(enemyName);
        EditorGUILayout.PropertyField(description);
        EditorGUILayout.PropertyField(detailedDescription);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Combat Stats", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxHealth);
        EditorGUILayout.PropertyField(baseAttack);
        EditorGUILayout.PropertyField(damageVariance);
        EditorGUILayout.PropertyField(evasionChance);
        EditorGUILayout.PropertyField(xpReward);
        EditorGUILayout.Space();
        // --- Behavior Section ---
        EditorGUILayout.LabelField("Behavior & Abilities", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(behavior);
        if (behavior.objectReferenceValue == null)
        {
            if (GUILayout.Button("Create New Behavior Asset"))
            {
                CreateAndAssignBehavior();
            }
        }
        EditorGUILayout.PropertyField(attacksOnSight);
        // --- NEW: Auto-Attack Section ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Auto-Attack Behavior", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        // --- Loot Drops Section ---
        EditorGUILayout.LabelField("Loot & Death", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lootDrops, true);
        if (GUILayout.Button("Create and Add New Loot Item"))
        {
            CreateAndAddAsset<Item>((Enemy)target, "lootDrops");
        }
        EditorGUILayout.Space();
        // --- Death Action Section ---
        EditorGUILayout.PropertyField(exitDirectionToReveal);
        serializedObject.ApplyModifiedProperties();
    }

    private void CreateAndAssignBehavior()
    {
        Enemy targetEnemy = (Enemy)target;
        EnemyBehavior newBehavior = CreateInstance<EnemyBehavior>();
        newBehavior.name = $"BH_{targetEnemy.name}";

        string directory = "Assets/Resources/Enemy Behaviors";
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newBehavior.name + ".asset"));

        AssetDatabase.CreateAsset(newBehavior, assetPath);

        behavior.objectReferenceValue = newBehavior;

        serializedObject.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created and assigned new EnemyBehavior at: {assetPath}");
    }

    private void CreateAndAddAsset<T>(Object targetObject, string listPropertyName) where T : ScriptableObject
    {
        T newAsset = CreateInstance<T>();
        string typeName = typeof(T).Name;
        newAsset.name = $"New {typeName} for {targetObject.name}";

        string directory = $"Assets/Resources/Items";
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newAsset.name + ".asset"));

        AssetDatabase.CreateAsset(newAsset, assetPath);

        SerializedObject so = new SerializedObject(targetObject);
        SerializedProperty listProperty = so.FindProperty(listPropertyName);

        listProperty.arraySize++;
        listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1).objectReferenceValue = newAsset;

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created '{assetPath}' and added it to the '{listPropertyName}' list on '{targetObject.name}'.");
    }
}