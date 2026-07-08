namespace TextEngine.EditorTools
{
    using TextEngine;

    using UnityEngine;
    using UnityEditor;
    using System.IO;
    using System.Collections.Generic;

    [CustomEditor(typeof(Location))]
    public class LocationEditor : Editor
    {
        private Dictionary<string, int> previousArraySizes = new Dictionary<string, int>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update(); // Always start with this
            // Draw core Location properties
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundMusic"));
            EditorGUILayout.Space();
            // Draw Shop Settings
            SerializedProperty isShopProp = serializedObject.FindProperty("isShop");
            EditorGUILayout.PropertyField(isShopProp, new GUIContent("Is Shop Location", "Enable to treat this location as a shop"));
            if (isShopProp.boolValue)
            {
                SerializedProperty shopInvProp = serializedObject.FindProperty("shopInventory");
                EditorGUILayout.PropertyField(shopInvProp, new GUIContent("Shop Inventory", "Items available to buy/sell at this shop"), true);
                HandleShopInventoryButtons(shopInvProp);
                EditorGUILayout.Space();
            }
            // --- Handle all lists with their specific controls ---
            HandleExitsList(); // Use a dedicated method for the unique Exits list
            HandleAssetList<Item>("itemsInLocation", "Items In Location");
            HandleAssetList<Interactable>("interactables", "Interactables");
            HandleAssetList<Character>("charactersInLocation", "Characters In Location");
            HandleAssetList<Enemy>("defaultEnemies", "Default Enemies In Location");
            serializedObject.ApplyModifiedProperties(); // Apply any changes
        }

        /// <summary>
        /// Draws create/add/delete controls for the shop inventory.
        /// </summary>
        private void HandleShopInventoryButtons(SerializedProperty listProperty)
        {
            // Add new Item button
            if (GUILayout.Button("Create and Add New Shop Item"))
            {
                CreateAndAddAssetToList<Item>(listProperty);
            }
        }

        /// <summary>
        /// A dedicated method to draw the Exits list and its unique button.
        /// </summary>
        private void HandleExitsList()
        {
            const string propertyName = "exits";
            const string label = "Exits";

            SerializedProperty listProperty = serializedObject.FindProperty(propertyName);

            // Store and check array size to detect when the '+' button is clicked
            if (!previousArraySizes.ContainsKey(propertyName))
            {
                previousArraySizes[propertyName] = listProperty.arraySize;
            }
            int previousSize = previousArraySizes[propertyName];

            EditorGUILayout.PropertyField(listProperty, true);

            // If a new exit was added, reset its values
            if (listProperty.arraySize > previousSize)
            {
                ResetNewExit(listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1));
            }
            previousArraySizes[propertyName] = listProperty.arraySize;

            // Button to create and assign a new Location asset to the last empty exit
            if (GUILayout.Button("Create & Assign Location to Last Empty Exit"))
            {
                CreateAndAssignNewLocation(listProperty);
            }
            EditorGUILayout.Space();
        }

        /// <summary>
        /// A specialized method for lists of ScriptableObjects that includes a "Create and Add" button.
        /// </summary>
        private void HandleAssetList<T>(string propertyName, string label) where T : ScriptableObject
        {
            SerializedProperty listProperty = serializedObject.FindProperty(propertyName);

            EditorGUILayout.PropertyField(listProperty, new GUIContent(label), true);

            // "Create and Add" button for this asset type
            if (GUILayout.Button($"Create and Add New {typeof(T).Name}"))
            {
                CreateAndAddAssetToList<T>(listProperty);
            }
            EditorGUILayout.Space();
        }

        private void CreateAndAddAssetToList<T>(SerializedProperty listProperty) where T : ScriptableObject
        {
            serializedObject.Update();

            T newAsset = CreateInstance<T>();
            newAsset.name = $"New {typeof(T).Name}";

            string baseFolder = EditorPaths.ContentRoot;
            string typeFolder = typeof(T).Name.EndsWith("y")
                ? typeof(T).Name.Substring(0, typeof(T).Name.Length - 1) + "ies"
                : typeof(T).Name + "s";
            string directory = Path.Combine(baseFolder, typeFolder);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newAsset.name + ".asset"));
            AssetDatabase.CreateAsset(newAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            listProperty.arraySize++;
            listProperty
                .GetArrayElementAtIndex(listProperty.arraySize - 1)
                .objectReferenceValue = newAsset;

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(target);
            Repaint();

            Debug.Log($"Created '{assetPath}' and added it to the list.");
        }


        private void ResetNewExit(SerializedProperty exitProperty)
        {
            // Unity's '+' button clones the previous element, so EVERY field
            // must be reset — a cloned InstaDeath or locked exit would silently
            // carry that configuration into the innocent new exit.
            exitProperty.FindPropertyRelative("direction").stringValue = "";
            exitProperty.FindPropertyRelative("destination").objectReferenceValue = null;
            exitProperty.FindPropertyRelative("isLocked").boolValue = false;
            exitProperty.FindPropertyRelative("keyToUnlock").objectReferenceValue = null;
            exitProperty.FindPropertyRelative("lockedDescription").stringValue = "";
            exitProperty.FindPropertyRelative("isHidden").boolValue = false;
            exitProperty.FindPropertyRelative("exitAction").enumValueIndex = (int)ExitActionType.None;
            exitProperty.FindPropertyRelative("deathMessage").stringValue = "";
            exitProperty.FindPropertyRelative("blockingItem").objectReferenceValue = null;
            exitProperty.FindPropertyRelative("blockedMessage").stringValue = "";
            exitProperty.FindPropertyRelative("targetLocation").objectReferenceValue = null;
            exitProperty.FindPropertyRelative("itemsToReveal").ClearArray();
            exitProperty.FindPropertyRelative("stateChangeMessage").stringValue = "";
        }

        private void CreateAndAssignNewLocation(SerializedProperty exitsProperty)
        {
            SerializedProperty targetExitProperty = null;
            for (int i = exitsProperty.arraySize - 1; i >= 0; i--)
            {
                var prop = exitsProperty.GetArrayElementAtIndex(i);
                if (prop.FindPropertyRelative("destination").objectReferenceValue == null)
                {
                    targetExitProperty = prop.FindPropertyRelative("destination");
                    break;
                }
            }

            if (targetExitProperty == null)
            {
                Debug.LogWarning("No empty 'destination' found in the exits list.");
                return;
            }

            Location newLocation = CreateInstance<Location>();
            newLocation.name = "New Location";

            string directory = EditorPaths.Folder("Locations");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newLocation.name + ".asset"));

            AssetDatabase.CreateAsset(newLocation, assetPath);
            AssetDatabase.SaveAssets();

            targetExitProperty.objectReferenceValue = newLocation;
            Debug.Log($"Created new Location at '{assetPath}' and assigned it.");
        }
    }
}
