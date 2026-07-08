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

            // With no registry the flag lists fall back to plain text fields —
            // never inject placeholder strings that could be written into data.
            flagOptions = flagRegistry != null ? flagRegistry.flags.ToArray() : new string[0];
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

                if (flagOptions.Length == 0)
                {
                    EditorGUILayout.HelpBox("No FlagRegistry asset found. Create one via Assets ▸ Create ▸ Text Adventure ▸ Flag Registry to get flag dropdowns; plain text fields are shown for now.", MessageType.Warning);
                }

                // Loop through each element in the list and draw it as a dropdown.
                for (int i = 0; i < listProperty.arraySize; i++)
                {
                    SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
                    string currentValue = element.stringValue;

                    // Without a registry, fall back to editing the raw string.
                    if (flagOptions.Length == 0)
                    {
                        EditorGUILayout.PropertyField(element, new GUIContent($"Element {i}"));
                        continue;
                    }

                    int currentIndex = System.Array.IndexOf(flagOptions, currentValue);
                    if (currentIndex >= 0)
                    {
                        int newIndex = EditorGUILayout.Popup($"Element {i}", currentIndex, flagOptions);
                        if (newIndex != currentIndex)
                        {
                            element.stringValue = flagOptions[newIndex];
                        }
                    }
                    else
                    {
                        // The stored value is empty or missing from the registry.
                        // Show it as an explicit placeholder entry so drawing the
                        // inspector never silently rewrites data — the value only
                        // changes when the user actively picks a real flag.
                        string placeholder = string.IsNullOrEmpty(currentValue)
                            ? "(select a flag)"
                            : $"(missing from registry: {currentValue})";
                        string[] options = new string[flagOptions.Length + 1];
                        options[0] = placeholder;
                        System.Array.Copy(flagOptions, 0, options, 1, flagOptions.Length);
                        int picked = EditorGUILayout.Popup($"Element {i}", 0, options);
                        if (picked > 0)
                        {
                            element.stringValue = flagOptions[picked - 1];
                        }
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

            Debug.Log($"[Text Engine] Created and linked new DialogueNode at: {assetPath}");
        }
    }
}
