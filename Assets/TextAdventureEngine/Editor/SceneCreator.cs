namespace TextEngine.EditorTools
{
    using System.Linq;
    using TextEngine;
    using TMPro;
    using UnityEditor;
    using UnityEditor.Events;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    /// <summary>
    /// One-click game scene setup: Tools ▸ Text Engine ▸ Create Game Scene
    /// builds a complete, wired scene — camera, canvas, scrolling output view,
    /// input field, EventSystem (matching the active input backend), a fully
    /// referenced GameController, and a SoundManager with its events hooked up.
    /// New users go from an empty project to a playable engine in one click.
    /// </summary>
    public static class SceneCreator
    {
        // Palette matching the demo's terminal look.
        private static readonly Color BackgroundColor = new Color(0.04f, 0.04f, 0.06f);
        private static readonly Color InputBackgroundColor = new Color(0.10f, 0.10f, 0.14f);
        private static readonly Color TextColor = new Color(0.85f, 0.87f, 0.95f);

        [MenuItem("Tools/Text Engine/Create Game Scene")]
        public static void CreateGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A camera so the game view isn't blank (the UI is overlay-rendered).
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;

            var controller = BuildEngineRig();

            Selection.activeGameObject = controller.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Game Scene", "TextEngineGame", "unity", "Choose where to save the new game scene");
            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.SaveScene(scene, path);
            }

            Debug.Log("[Text Engine] Game scene created. Next steps: assign your starting Locations on the GameController (demo locations were auto-assigned if present) and press Play.");
        }

        /// <summary>
        /// Builds the UI + engine objects into the active scene and returns the
        /// wired GameController.
        /// </summary>
        private static GameController BuildEngineRig()
        {
            // --- Canvas ---
            var canvasGo = new GameObject("Text Engine UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // --- Background ---
            var background = CreateUIObject("Background", canvasGo.transform);
            Stretch(background, Vector2.zero, Vector2.zero);
            background.gameObject.AddComponent<Image>().color = BackgroundColor;

            // --- Scrolling output area ---
            var scrollGo = CreateUIObject("Output Scroll View", canvasGo.transform);
            Stretch(scrollGo, new Vector2(20, 90), new Vector2(-20, -20));
            var scrollRect = scrollGo.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            var viewport = CreateUIObject("Viewport", scrollGo);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scrollRect.viewport = viewport;

            // The display text doubles as the scroll content: a ContentSizeFitter
            // grows it downward as the transcript grows.
            var textGo = CreateUIObject("Display Text", viewport);
            textGo.anchorMin = new Vector2(0, 1);
            textGo.anchorMax = new Vector2(1, 1);
            textGo.pivot = new Vector2(0.5f, 1);
            textGo.offsetMin = new Vector2(10, 0);
            textGo.offsetMax = new Vector2(-10, 0);
            var displayText = textGo.gameObject.AddComponent<TextMeshProUGUI>();
            displayText.fontSize = 24;
            displayText.color = TextColor;
            displayText.alignment = TextAlignmentOptions.TopLeft;
            displayText.richText = true;
            displayText.text = "";
            var fitter = textGo.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = textGo;

            // --- Input field ---
            var inputGo = CreateUIObject("Command Input", canvasGo.transform);
            inputGo.anchorMin = new Vector2(0, 0);
            inputGo.anchorMax = new Vector2(1, 0);
            inputGo.pivot = new Vector2(0.5f, 0);
            inputGo.offsetMin = new Vector2(20, 20);
            inputGo.offsetMax = new Vector2(-20, 70);
            var inputImage = inputGo.gameObject.AddComponent<Image>();
            inputImage.color = InputBackgroundColor;
            var inputField = inputGo.gameObject.AddComponent<TMP_InputField>();

            var textArea = CreateUIObject("Text Area", inputGo);
            Stretch(textArea, new Vector2(12, 6), new Vector2(-12, -6));
            var textAreaMask = textArea.gameObject.AddComponent<RectMask2D>();

            var inputTextGo = CreateUIObject("Text", textArea);
            Stretch(inputTextGo, Vector2.zero, Vector2.zero);
            var inputText = inputTextGo.gameObject.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 24;
            inputText.color = TextColor;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            inputText.textWrappingMode = TextWrappingModes.NoWrap;

            var placeholderGo = CreateUIObject("Placeholder", textArea);
            Stretch(placeholderGo, Vector2.zero, Vector2.zero);
            var placeholder = placeholderGo.gameObject.AddComponent<TextMeshProUGUI>();
            placeholder.fontSize = 24;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.35f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.text = "Type a command…";

            inputField.textViewport = textArea;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            // --- EventSystem matching the active input backend ---
            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
#if TEXTENGINE_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
            eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemGo.AddComponent<StandaloneInputModule>();
#endif

            // --- GameController, fully wired ---
            var controllerGo = new GameObject("Game Controller");
            var controller = controllerGo.AddComponent<GameController>();
            controller.displayText = displayText;
            controller.inputField = inputField;
            controller.scrollRect = scrollRect;
            controller.engineSettings = FindOrCreateEngineSettings();
            controller.introText = "Welcome to your new adventure!";

            // Point both start locations at any existing Location so the scene
            // runs immediately; the designer swaps these for their own rooms.
            var anyLocation = FindAsset<Location>();
            controller.tutorialStartLocation = anyLocation;
            controller.mainGameStartLocation = anyLocation;
            if (anyLocation == null)
            {
                Debug.LogWarning("[Text Engine] No Location assets exist yet — create one (Assets ▸ Create ▸ Text Adventure ▸ Location) and assign it as the starting location on the GameController.");
            }

            // --- SoundManager wired to the controller's events ---
            var soundGo = new GameObject("Sound Manager");
            var soundManager = soundGo.AddComponent<SoundManager>();
            var musicSource = soundGo.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            var sfxSource = soundGo.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            soundManager.musicSource = musicSource;
            soundManager.sfxSource = sfxSource;
            UnityEventTools.AddPersistentListener(controller.onLocationChanged, soundManager.OnLocationChanged);
            UnityEventTools.AddPersistentListener(controller.onItemTaken, soundManager.OnItemTaken);

            return controller;
        }

        private static RectTransform CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            return rect;
        }

        // Anchors the rect to fully stretch within its parent, with pixel
        // offsets from each edge (min = left/bottom, max = right/top).
        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static T FindAsset<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(a => a != null);

        private static EngineSettings FindOrCreateEngineSettings()
        {
            var settings = FindAsset<EngineSettings>();
            if (settings != null) return settings;

            settings = ScriptableObject.CreateInstance<EngineSettings>();
            string root = EditorPaths.ContentRoot;
            if (!System.IO.Directory.Exists(root)) System.IO.Directory.CreateDirectory(root);
            string path = AssetDatabase.GenerateUniqueAssetPath(root + "/Engine Settings.asset");
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Text Engine] Created a default Engine Settings asset at '{path}'.");
            return settings;
        }
    }
}
