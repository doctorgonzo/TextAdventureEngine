namespace TextEngine.EditorTools
{
    using TextEngine;

    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(DialogueNode))]
    public class DialogueNodeEditor : Editor
    {
        private FlagRegistry flagRegistry;
        private string[] flagOptions;

        private void OnEnable()
        {
            // Find and load the Flag Registry asset when the editor is enabled.
            flagRegistry = AssetDatabase.FindAssets("t:FlagRegistry")
                .Select(guid => AssetDatabase.LoadAssetAtPath<FlagRegistry>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault();

            if (flagRegistry != null)
            {
                flagOptions = flagRegistry.flags.ToArray();
            }
            else
            {
                // Provide a fallback if the registry isn't found.
                flagOptions = new string[] { "ERROR: FlagRegistry asset not found!" };
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw the default properties
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueText"));
            EditorGUILayout.Space();
            //EditorGUILayout.LabelField("Node Actions & Conditions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredItemForSuccess"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("failureNode"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("successActions"));

            EditorGUILayout.Space();

            // --- START OF NEW LOGIC ---
            // Manually draw the flag lists using our custom dropdown helper method.
            DrawFlagList(serializedObject.FindProperty("requiredFlags"), "Required Flags");
            DrawFlagList(serializedObject.FindProperty("flagsToSet"), "Flags To Set");
            // --- END OF NEW LOGIC ---

            EditorGUILayout.Space();
            //EditorGUILayout.LabelField("Player Responses", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerResponses"), true);

            // ... (the rest of your button logic for creating new nodes) ...
            DrawResponseButtons();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// A custom method to draw a list of strings as dropdown menus,
        /// including manual Add (+) and Remove (-) buttons.
        /// </summary>
        private void DrawFlagList(SerializedProperty listProperty, string label)
        {
            // Draw the main foldout for the list.
            listProperty.isExpanded = EditorGUILayout.Foldout(listProperty.isExpanded, label, true);

            if (listProperty.isExpanded)
            {
                EditorGUI.indentLevel++;

                // Draw the "Size" field. A user can still type the size here if they want.
                EditorGUILayout.PropertyField(listProperty.FindPropertyRelative("Array.size"));

                // Loop through each element in the list and draw it as a dropdown.
                for (int i = 0; i < listProperty.arraySize; i++)
                {
                    SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
                    string currentValue = element.stringValue;

                    int currentIndex = System.Array.IndexOf(flagOptions, currentValue);
                    if (currentIndex < 0) { currentIndex = 0; } // Default to first entry if not found

                    int newIndex = EditorGUILayout.Popup($"Element {i}", currentIndex, flagOptions);

                    if (newIndex != currentIndex)
                    {
                        element.stringValue = flagOptions[newIndex];
                    }
                }

                // --- START OF FIX ---
                // Manually draw the Add and Remove buttons in the bottom-right corner.
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // Pushes the buttons to the right side

                if (GUILayout.Button("+", GUILayout.Width(25)))
                {
                    listProperty.arraySize++;
                }

                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    // Prevent the size from going below zero.
                    if (listProperty.arraySize > 0)
                    {
                        listProperty.arraySize--;
                    }
                }
                EditorGUILayout.EndHorizontal();
                // --- END OF FIX ---

                EditorGUI.indentLevel--;
            }
        }

        private void DrawResponseButtons()
        {
            DialogueNode node = (DialogueNode)target;
            SerializedProperty responsesProperty = serializedObject.FindProperty("playerResponses");
            for (int i = 0; i < responsesProperty.arraySize; i++)
            {
                SerializedProperty responseProperty = responsesProperty.GetArrayElementAtIndex(i);
                SerializedProperty nextNodeProperty = responseProperty.FindPropertyRelative("nextNode");

                if (nextNodeProperty.objectReferenceValue == null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Create & Link New Node", GUILayout.Width(200), GUILayout.Height(20)))
                    {
                        CreateNewDialogueNode(node, nextNodeProperty);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void CreateNewDialogueNode(DialogueNode parentNode, SerializedProperty nextNodeProperty)
        {
            // 1. Create a new DialogueNode instance
            DialogueNode newNode = CreateInstance<DialogueNode>();
            newNode.name = "New Dialogue Node"; // A temporary name
            string directory = EditorPaths.Folder("Dialogue");
            if (!Directory.Exists(directory))
            {
                // Create the folder if it doesn't exist
                Directory.CreateDirectory(directory);
            }
            // 2. Determine a unique path to save the new asset
            string path = AssetDatabase.GetAssetPath(parentNode);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newNode.name + ".asset"));

            // 3. Create the asset file on disk
            AssetDatabase.CreateAsset(newNode, assetPath);
            AssetDatabase.SaveAssets();

            // 4. Link the new node back to the response property
            nextNodeProperty.objectReferenceValue = newNode;

            Debug.Log($"Created and linked new DialogueNode at: {assetPath}");
        }
    }
}
