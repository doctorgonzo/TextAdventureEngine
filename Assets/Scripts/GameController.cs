using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// TextBlock and TextType are defined in TextRenderer.cs.
public enum GameState { Intro, Playing, Dialogue, MiniGame, MinigamePostlude, GameOver, SkillTraining, Shop }

public class EnemyInstance
{
    public Enemy enemyBlueprint; // The ScriptableObject template
    public int currentHealth;
    public float autoAttackTimer;

    public EnemyInstance(Enemy blueprint)
    {
        enemyBlueprint = blueprint;
        currentHealth = blueprint.maxHealth;
    }
}

// GameController is split across partial files:
//   GameController.cs          — lifecycle, input parsing, world/UI orchestration
//   GameController.Combat.cs   — attack resolution, enemy AI, status effects
//   GameController.Dialogue.cs — conversation flow and dialogue-node actions
// Combat and dialogue are kept as partials (rather than standalone classes)
// because they mutate a large amount of shared player state; splitting the
// files separates the concerns without threading that state through an
// injected context.
public partial class GameController : MonoBehaviour
{
    [Header("Player Stats")]
    public PlayerStats playerStats;
    private int playerCurrentHealth;
    public int playerDamageVariance = 2;
    private float playerDrunkenness = 0.0f;
    private bool wasDrunkLastFrame = false;
    public float drunkennessSoberRate = 0.05f;
    [Header("Game Text")]
    [TextArea(5, 10)]
    public string introText;
    public TextMeshProUGUI displayText;
    public TMP_InputField inputField;
    private WobblyText wobblyTextEffect;
    [Header("Engine Settings")]
    public int nameCharacterLimit = 25;
    public float charactersPerSecond = 50f;
    public ScrollRect scrollRect;
    public bool bypassTutorial;
    [Header("Engine Configuration")]
    [Tooltip("Drag your EngineSettings ScriptableObject asset here.")]
    public EngineSettings engineSettings;
    [Header("Starting Locations")]
    public Location tutorialStartLocation; 
    public Location mainGameStartLocation;

    private GameState currentGameState;
    private string playerName;
    private WorldState world;
    // Forwarding accessors so the rest of GameController reads/mutates the
    // world model without caring that WorldState now owns it. These return the
    // live collections by reference, so indexing, Add/Remove, and Clear all
    // operate on the real state.
    private Dictionary<Location, List<ItemInstance>> roomItemsState => world.roomItemsState;
    private Dictionary<Exit, bool> exitLockedState => world.exitLockedState;
    private Dictionary<Exit, bool> exitVisibilityState => world.exitVisibilityState;
    private Dictionary<Interactable, string> interactableStates => world.interactableStates;
    private Dictionary<Location, List<EnemyInstance>> roomEnemiesState => world.roomEnemiesState;
    private Dictionary<Location, List<Character>> roomCharactersState => world.roomCharactersState;
    private Dictionary<string, bool> worldFlags => world.worldFlags;
    private Dictionary<Location, List<ItemInstance>> runtimeShopInventories => world.runtimeShopInventories;
    private Dictionary<Location, List<Interactable>> roomInteractablesState => world.roomInteractablesState;
    private Location[] allLocations => world.allLocations;
    private Character activeConversationCharacter;
    private Location currentLocation;
    private Location previousLocation;
    private Location activeShop;
    private Action[] allActions;
    private List<ItemInstance> playerInventory = new List<ItemInstance>();
    public List<ActiveQuest> activeQuests = new List<ActiveQuest>();
    public List<Quest> completedQuests = new List<Quest>();
    private List<ActiveStatusEffect> activeStatusEffects = new List<ActiveStatusEffect>();
    private const string playerInputColor = "#88FFFF"; // Light Cyan
    private const string gameResponseColor = "#FFFF88"; // Light Yellow
    private const string keywordColor = "#FF88FF"; // Magenta
    private const string enemyColor = "#FF8888";
    private DialogueNode currentDialogueNode;
    private ItemInstance equippedWeapon;
    private ItemInstance equippedArmor;
    private List<string> commandHistory = new List<string>();
    private int historyIndex = 0;
    private const int C4_ROWS = 6;
    private const int C4_COLS = 7;
    private int[,] connectFourBoard;
    private bool isPlayerTurn = true;
    // Display name of whoever the minigame was started against.
    private string minigameOpponentName = "Your opponent";
    private bool isPlayerStunned = false;
    private List<Skill> learnedSkills = new List<Skill>();
    private Skill[] allSkills;
    private Enemy[] allEnemies => world.allEnemies;
    private Item[] allItems => world.allItems;
    private Character activeTrainer;
    private CustomAction[] allCustomActions;
    private TextRenderer textRenderer;
    private SaveSystem saveSystem;
    // Built-in verb keyword -> handler, built once in Awake. Each handler
    // receives the (article-stripped, lowercased) noun phrase after the verb.
    private Dictionary<string, System.Action<string>> verbHandlers;
    // Single-letter shortcuts the parser expands before matching exit directions.
    private static readonly Dictionary<string, string> directionAliases = new Dictionary<string, string>
    {
        { "n", "north" }, { "s", "south" }, { "e", "east" }, { "w", "west" },
        { "u", "up" }, { "d", "down" }
    };

    #region Game Events
    [System.Serializable]
    public class ItemEvent : UnityEvent<Item> { }

    [System.Serializable]
    public class LocationEvent : UnityEvent<Location> { }

    [System.Serializable]
    public class EnemyInstanceEvent : UnityEvent<EnemyInstance> { }

    [Header("Game Events")]
    [Tooltip("Fired when the player successfully takes an item.")]
    public ItemEvent onItemTaken;

    [Tooltip("Fired when the player successfully drops an item.")]
    public ItemEvent onItemDropped;

    [Tooltip("Fired when the player moves to a new location.")]
    public LocationEvent onLocationChanged;

    [Tooltip("Fired when a combat encounter with an enemy is over.")]
    public EnemyInstanceEvent onEnemyDefeated;
    #endregion

    #region Unity Lifecycle Methods
    void Awake()
    {
        wobblyTextEffect = displayText.GetComponent<WobblyText>();
        if (wobblyTextEffect == null)
        {
            // If it doesn't exist, add it automatically.
            wobblyTextEffect = displayText.gameObject.AddComponent<WobblyText>();
        }
        // Set up the extracted subsystems, injecting the references this
        // component already has wired up in the inspector.
        textRenderer = new TextRenderer(this, displayText, inputField, scrollRect,
            charactersPerSecond, ProcessPlayerName, playerInputColor, gameResponseColor);
        saveSystem = new SaveSystem();
        // WorldState loads its own location/enemy/item catalogs from Resources.
        world = new WorldState();
        // Actions and skills are used by the parser/skill systems, not the
        // world model, so GameController still owns those catalogs.
        allActions = Resources.LoadAll<Action>("Actions");
        allCustomActions = Resources.LoadAll<CustomAction>("Actions/Custom");
        allSkills = Resources.LoadAll<Skill>("Skills");
        BuildVerbHandlers();
        // Optional: Log a message to confirm everything loaded correctly.
        Debug.Log($"Loaded {allActions.Length} actions and {allLocations.Length} locations.");
    }

    void Start()
    {
        // First, check if we are loading a game from the main menu.
        if (GameLoader.loadGameOnStart)
        {
            HandleLoad();
        }
        // Next, check if the developer wants to bypass the tutorial.
        else if (bypassTutorial)
        {
            LogText("Bypassing tutorial and starting main game...");
            // Set the player's location to the main game start.
            currentLocation = mainGameStartLocation;
            // Initialize the game state (this will display the location).
            InitializeGame();
        }
        // Otherwise, start a normal new game.
        else
        {
            currentGameState = GameState.Intro;
            inputField.characterLimit = nameCharacterLimit;
            // Set the player's location to the tutorial start.
            currentLocation = tutorialStartLocation;
            string initialText = introText + "\n\nWhat is your name?";
            PrintToScreen(initialText);
        }

        // This part runs regardless of how the game started.
        inputField.onSubmit.AddListener(ProcessInput);
        inputField.ActivateInputField();
    }

    void Update()
    {
        if (inputField.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                // Move backward in history
                historyIndex--;
                if (historyIndex < 0) { historyIndex = 0; } // Don't go past the beginning
                if (commandHistory.Count > 0)
                {
                    inputField.text = commandHistory[historyIndex];
                    inputField.caretPosition = inputField.text.Length; // Move cursor to end
                }
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                // Move forward in history
                historyIndex++;
                if (historyIndex > commandHistory.Count) { historyIndex = commandHistory.Count; } // Don't go past the end
                if (historyIndex < commandHistory.Count)
                {
                    inputField.text = commandHistory[historyIndex];
                    inputField.caretPosition = inputField.text.Length; // Move cursor to end
                }
                else
                {
                    // If we're at the end of the history, clear the input field
                    inputField.text = "";
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.Space) && textRenderer.IsProcessing)
        {
            textRenderer.SkipToEnd();
        }
        if (engineSettings.useDrunkennessSystem && playerDrunkenness > 0)
        {
            // Decrease drunkenness over time using Time.deltaTime for frame-rate independence.
            playerDrunkenness -= drunkennessSoberRate * Time.deltaTime;
            playerDrunkenness = Mathf.Max(0, playerDrunkenness); // Clamp it so it doesn't go below 0.
            // Set the flag that we are currently drunk.
            wasDrunkLastFrame = true;
        }
        else if (wasDrunkLastFrame)
        {
            // If we were drunk last frame but are not anymore, it means we just sobered up.
            LogText("Your head starts to clear.", TextType.GameResponse);
            wasDrunkLastFrame = false; // Reset the flag so the message only appears once.
        }
        if (wobblyTextEffect != null)
        {
            wobblyTextEffect.wobbleIntensity = engineSettings.useDrunkennessSystem ? this.playerDrunkenness : 0;
        }
        if (engineSettings.useStatusEffectSystem)
        {
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
            {
            if (activeStatusEffects[i].Tick(Time.deltaTime, this))
                {
                LogText($"{activeStatusEffects[i].effect.effectName} has worn off.", TextType.GameResponse);
                activeStatusEffects.RemoveAt(i);
                }
            }
        }
    }
    #endregion

    void InitializeGame()
    {
        inputField.characterLimit = 0;
        currentGameState = GameState.Playing;
        playerInventory = new List<ItemInstance>();
        equippedWeapon = null;
        equippedArmor = null;
        playerStats = new PlayerStats();
        world.BuildRuntimeState();
        // Compute derived stats first, THEN fill current health/mana from the
        // resulting maxima (maxHealth/maxMana are 0 until this runs).
        RecalculatePlayerStats();
        playerCurrentHealth = playerStats.maxHealth;
        playerStats.currentMana = playerStats.maxMana;
        DisplayLocation();
        playerInventory.RemoveAll(item => item == null);
    }

    void PrintToScreen(string textToPrint, bool useTypewriter = true)
    {
        textRenderer.Print(textToPrint, useTypewriter);
    }

    string ProcessPlayerName(string text)
    {
        if (text.Contains("{PLAYER_NAME}"))
        {
            if (!string.IsNullOrEmpty(playerName)) return text.Replace("{PLAYER_NAME}", playerName);
            else return text.Replace("{PLAYER_NAME}", "stranger");
        }
        return text;
    }

    void ProcessInput(string inputText)
    {
        // 1. Sanitize the input first.
        inputText = inputText.TrimStart();
        // 2. Handle the GameOver state first.
        if (currentGameState == GameState.GameOver)
        {
            LogText("The game is over. Please restart.", TextType.GameResponse);
            inputField.text = "";
            inputField.ActivateInputField();
            return;
        }
        if (currentGameState == GameState.Playing && !string.IsNullOrWhiteSpace(inputText))
        {
            // Add the command to our history list.
            // We only add it if it's not a duplicate of the last command.
            if (commandHistory.Count == 0 || commandHistory[commandHistory.Count - 1] != inputText)
            {
                commandHistory.Add(inputText);
            }
            // IMPORTANT: Reset the history index to the end of the list.
            // This ensures that when you press 'up', you start from the most recent command.
            historyIndex = commandHistory.Count;
        }
        // If we're in a state that should echo commands...
        if (currentGameState == GameState.Playing ||
           (currentGameState == GameState.Intro && !string.IsNullOrWhiteSpace(inputText)))
        {
            // Instantly append the player's command to the display text,
            // bypassing the queue entirely so it feels responsive.
            textRenderer.ShowImmediate("> " + inputText, TextType.PlayerInput);
        }
        // 3. Use a switch to handle the logic for each specific game state.
        switch (currentGameState)
        {
            case GameState.Intro:
                if (string.IsNullOrWhiteSpace(inputText)) { inputField.ActivateInputField(); return; }
                playerName = inputText;
                // The echo is already handled above, so we just log the response.
                LogText("An unusual name. Very well, " + playerName + ", your adventure begins...");
                InitializeGame();
                break;
            case GameState.Playing:
                if (string.IsNullOrWhiteSpace(inputText)) { inputField.ActivateInputField(); return; }
                // The echo is handled above, so we just parse the command.
                ParsePlayerCommand(inputText);
                break;
            case GameState.Dialogue:
                if (string.IsNullOrWhiteSpace(inputText)) { inputField.ActivateInputField(); return; }
                HandleDialogueResponse(inputText); // Dialogue responses are handled differently.
                break;
            case GameState.MiniGame:
                if (string.IsNullOrWhiteSpace(inputText)) { inputField.ActivateInputField(); return; }
                HandleMinigameMove(inputText);
                break;
            case GameState.MinigamePostlude:
                EndConnectFour();
                break;
            case GameState.SkillTraining:
                if (string.IsNullOrWhiteSpace(inputText)) { inputField.ActivateInputField(); return; }
                string[] words = inputText.ToLower().Split(' ');
                if (words[0] == "buy" && words.Length > 1)
                {
                    string skillName = string.Join(" ", words.Skip(1).Where(w => w != "a" && w != "an" && w != "the"));
                    HandleBuySkill(skillName);
                }
                else if (words[0] == "leave")
                {
                    ExitSkillTrainingMode();
                }
                else
                {
                    LogText("Invalid command. Please use 'buy <skill name>' or 'leave'.");
                }
                break;
            case GameState.Shop:
                if (string.IsNullOrWhiteSpace(inputText)) { inputField.ActivateInputField(); return; }
                string[] words1 = inputText.ToLower().Split(' ');
                string command = words1[0];

                if (command == "buy" && words1.Length > 1)
                {
                    string itemName = string.Join(" ", words1.Skip(1).Where(w => w != "a" && w != "an" && w != "the"));
                    HandleBuy(itemName); // Your existing HandleBuy function
                    DisplayShopInventory(); // Refresh the shop view after the action
                }
                else if (command == "sell" && words1.Length > 1)
                {
                    string itemName = string.Join(" ", words1.Skip(1).Where(w => w != "a" && w != "an" && w != "the"));
                    HandleSell(itemName); // Your existing HandleSell function
                    DisplayShopInventory(); // Refresh the shop view
                }
                else if (command == "leave")
                {
                    ExitShopMode();
                }
                else
                {
                    LogText("Invalid command. Please use 'buy <item>', 'sell <item>', or 'leave'.");
                }
                break;
        }
        // 4. Clear the input field for the next command.
        inputField.text = "";
        inputField.ActivateInputField();
    }

    // Maps every built-in verb keyword to its handler. Action assets still
    // control which keyword (and synonyms) the player can type; this table is
    // what the resolved keyword dispatches to. Verbs with no entry here fall
    // through to the data-driven CustomAction assets.
    void BuildVerbHandlers()
    {
        verbHandlers = new Dictionary<string, System.Action<string>>
        {
            { "go", AttemptToMove },
            { "look", p => HandleLook(StripLeadingWord(p, "at")) },
            { "talk", p => HandleTalk(StripLeadingWord(p, "to")) },
            { "use", HandleUseCommand },
            { "take", HandleTake },
            { "drop", HandleDrop },
            { "push", p => HandleInteract("push", p) },
            { "pull", p => HandleInteract("pull", p) },
            { "flush", p => HandleInteract("flush", p) },
            { "activate", p => HandleInteract("activate", p) },
            { "attack", HandleAttack },
            { "cast", HandleCast },
            { "inventory", _ => HandleInventory() },
            { "equip", HandleEquip },
            { "unequip", HandleUnequip },
            { "equipment", _ => HandleEquipment() },
            { "save", _ => HandleSave() },
            { "load", _ => HandleLoad() },
            { "help", _ => HandleHelp() },
            { "quests", _ => HandleQuests() },
            { "status", _ => HandleStatus() },
            { "char", _ => HandleChar() },
            { "skills", HandleSkills },
            { "learn", HandleLearnSkill },
            { "balance", _ => HandleBalance() },
            { "buy", HandleBuy },
            { "sell", HandleSell },
        };
    }

    // Removes a leading preposition, e.g. "look at chest" -> "chest".
    static string StripLeadingWord(string phrase, string word)
    {
        if (phrase == word) return "";
        return phrase.StartsWith(word + " ") ? phrase.Substring(word.Length + 1) : phrase;
    }

    // Splits "use <item> [on <target>]" into its two noun phrases.
    void HandleUseCommand(string nounPhrase)
    {
        var parts = nounPhrase.Split(new[] { " on " }, 2, System.StringSplitOptions.None);
        if (parts.Length == 2) HandleUse(parts[0], parts[1]);
        else HandleUse(nounPhrase, null);
    }

    public void ParsePlayerCommand(string inputText)
    {
        string[] words = inputText.ToLower().Split(' ');
        string verb = words[0];
        // Resolve the typed verb (or a synonym) to an Action asset's keyword.
        string actionKeyword = null;
        foreach (Action action in allActions) { if (action.keyword.ToLower() == verb || action.synonyms.Contains(verb)) { actionKeyword = action.keyword.ToLower(); break; } }
        // Build the noun phrase, dropping articles so "take the lantern" works.
        string nounPhrase = string.Join(" ", words.Skip(1)
            .Where(w => w != "a" && w != "an" && w != "the" && w.Length > 0));
        if (actionKeyword != null && verbHandlers.TryGetValue(actionKeyword, out var handler))
        {
            handler(nounPhrase);
            return;
        }
        // Not a built-in verb: check the data-driven CustomAction assets.
        // Matching the raw verb too means a CustomAction works on its own,
        // without also needing a mirror Action asset.
        string keywordToMatch = actionKeyword ?? verb;
        var customAction = allCustomActions.FirstOrDefault(a =>
            a.keyword.ToLower() == keywordToMatch ||
            a.synonyms.Any(s => s.ToLower() == verb));
        if (customAction != null)
        {
            HandleCustomAction(customAction, nounPhrase);
            return;
        }
        LogText("I don't understand the word '" + verb + "'.");
    }

    // Resolves a player-typed noun phrase against a collection, preferring an
    // exact (case-insensitive) name match, then names starting with the
    // phrase, then names containing it. When the best tier holds more than one
    // distinct name, prints a clarifying question and reports ambiguity so the
    // caller can stop without printing its own "not found" message.
    private T FindByNoun<T>(IEnumerable<T> source, System.Func<T, string> nameOf, string nounPhrase, out bool ambiguous)
    {
        ambiguous = false;
        if (string.IsNullOrEmpty(nounPhrase) || source == null) return default;
        string phrase = nounPhrase.ToLower();
        var named = source.Where(x => x != null && !string.IsNullOrEmpty(nameOf(x))).ToList();
        var matches = named.Where(x => nameOf(x).ToLower() == phrase).ToList();
        if (matches.Count == 0)
            matches = named.Where(x => nameOf(x).ToLower().StartsWith(phrase)).ToList();
        if (matches.Count == 0)
            matches = named.Where(x => nameOf(x).ToLower().Contains(phrase)).ToList();
        if (matches.Count == 0) return default;
        var distinctNames = matches.Select(nameOf).Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();
        if (distinctNames.Count > 1)
        {
            ambiguous = true;
            LogText($"Which do you mean: {string.Join(", ", distinctNames)}?");
            return default;
        }
        return matches[0];
    }

    void EnterShopMode(Location shop)
    {
        // Guard against a mis-authored OpenShop action in a non-shop room —
        // entering Shop state with nothing to display looks like a freeze.
        if (shop == null || !shop.isShop || !runtimeShopInventories.ContainsKey(shop))
        {
            Debug.LogWarning($"OpenShop was triggered in '{(shop != null ? shop.name : "null")}', which is not a shop. Set 'isShop' on the Location asset.");
            LogText("There is no shop here.");
            return;
        }
        currentGameState = GameState.Shop;
        activeShop = shop;
        DisplayShopInventory();
    }

    void DisplayShopInventory()
    {
        if (activeShop == null || !activeShop.isShop) return;
        // Get the live inventory from our runtime dictionary.
        List<ItemInstance> currentShopStock = runtimeShopInventories[activeShop];
        StringBuilder shopText = new StringBuilder();
        shopText.AppendLine("<color=yellow>--- Items for Sale ---</color>");
        shopText.AppendLine("---------------------------------");
        if (currentShopStock.Count == 0)
        {
            shopText.AppendLine("The shop is currently sold out.");
        }
        else
        {
            List<string> shopLines = BuildGroupedItemLines(currentShopStock,
                (item, count) => $"- {item.itemName}{(count > 1 ? " x" + count : "")} (Cost: {item.buyPrice} coins)");
            foreach (var line in shopLines)
            {
                shopText.AppendLine(line);
            }
        }
        shopText.AppendLine("---------------------------------");
        shopText.Append($"Your balance: {playerStats.currency} coins.\n");
        shopText.Append("Type 'buy <item name>' or 'sell <item name>', or 'leave' to exit.");
        PrintToScreen(shopText.ToString(), useTypewriter: false);
    }

    void ExitShopMode()
    {
        LogText("You leave the shop.");
        currentGameState = GameState.Playing;
        activeShop = null;
        DisplayLocation(useTypewriter: false); // Refresh the location description
    }

    void EnterSkillTrainingMode(Character trainer)
    {
        // Guard against a mis-authored OpenSkillShop action on a character
        // with nothing to teach.
        if (trainer == null || trainer.skillsToTeach == null || trainer.skillsToTeach.Count == 0)
        {
            Debug.LogWarning($"OpenSkillShop was triggered on '{(trainer != null ? trainer.name : "null")}', but they have no skillsToTeach.");
            LogText($"{(trainer != null ? trainer.characterName : "The trainer")} has nothing to teach you.");
            return;
        }
        currentGameState = GameState.SkillTraining;
        activeTrainer = trainer;
        DisplayTrainerSkills();
    }

    void DisplayTrainerSkills()
    {
        if (activeTrainer == null || activeTrainer.skillsToTeach.Count == 0) return;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<color=yellow>--- Skills Taught by {activeTrainer.characterName} ---</color>");
        sb.AppendLine("---------------------------------");
        foreach (var skill in activeTrainer.skillsToTeach)
        {
            if (learnedSkills.Contains(skill))
            {
                sb.AppendLine($"- {skill.skillName} <color=green>(Learned)</color>");
            }
            else
            {
                sb.AppendLine($"- {skill.skillName} (Cost: {skill.currencyCost} coins, Lvl Req: {skill.requiredLevel})");
                sb.AppendLine($"  <i>{skill.skillDescription}</i>");
            }
        }
        sb.AppendLine("---------------------------------");
        sb.Append($"Your balance: {playerStats.currency} coins.\n");
        sb.Append("Type 'buy <skill name>' to purchase, or 'leave' to exit.");
        PrintToScreen(sb.ToString(), useTypewriter: false);
    }

    void HandleBuySkill(string skillName)
    {
        if (activeTrainer == null) return;
        var skillToBuy = activeTrainer.skillsToTeach.FirstOrDefault(s => s.skillName.ToLower() == skillName.ToLower());
        if (skillToBuy == null)
        {
            LogText($"This trainer does not teach '{skillName}'.");
            return;
        }
        if (learnedSkills.Contains(skillToBuy))
        {
            LogText("You already know this skill.");
            return;
        }
        if (playerStats.level < skillToBuy.requiredLevel)
        {
            LogText($"You are not experienced enough. (Requires Level: {skillToBuy.requiredLevel})");
            return;
        }
        if (playerStats.currency < skillToBuy.currencyCost)
        {
            LogText($"You cannot afford this. (Requires Coins: {skillToBuy.currencyCost})");
            return;
        }
        playerStats.currency -= skillToBuy.currencyCost;
        learnedSkills.Add(skillToBuy);
        RecalculatePlayerStats();
        LogText($"You pay {skillToBuy.currencyCost} coins and learn {skillToBuy.skillName}!");
        DisplayTrainerSkills(); // Refresh the view
    }

    void ExitSkillTrainingMode()
    {
        LogText($"You finish your training with {activeTrainer.characterName}.");
        currentGameState = GameState.Playing;
        activeTrainer = null;
        DisplayLocation(useTypewriter: false);
    }

    #region MiniGame Logic
    void StartConnectFour()
    {
        currentGameState = GameState.MiniGame;
        connectFourBoard = new int[C4_ROWS, C4_COLS];
        isPlayerTurn = true; // Player always starts first in Connect Four.
        // The minigame is reached from dialogue, so the opponent is whoever
        // the player was talking to.
        minigameOpponentName = activeConversationCharacter != null
            ? activeConversationCharacter.characterName
            : "Your opponent";
        DisplayConnectFourBoard();
    }
    
    void TakeAITurn()
    {
        isPlayerTurn = false; // Switch to AI's turn
        StartCoroutine(AITurnCoroutine());
    }

    IEnumerator AITurnCoroutine()
    {
        // 1. "Think"
        LogText($"{minigameOpponentName} is thinking...", TextType.Narrative);
        yield return new WaitForSeconds(1.0f); // Wait for 1 second
        // 2. Determine Move
        List<int> validColumns = new List<int>();
        for (int col = 0; col < C4_COLS; col++)
        {
            if (connectFourBoard[0, col] == 0) validColumns.Add(col);
        }
        int chosenColumn = validColumns[Random.Range(0, validColumns.Count)];
        int chosenRow = -1;
        for (int row = C4_ROWS - 1; row >= 0; row--)
        {
            if (connectFourBoard[row, chosenColumn] == 0) { chosenRow = row; break; }
        }
        // 3. Place Piece
        connectFourBoard[chosenRow, chosenColumn] = 2; // 2 for AI
        // 4. Display Board
        DisplayConnectFourBoard();
        // 5. Check for Win/Loss
        if (CheckForWin(chosenRow, chosenColumn, 2))
        {
            ProcessMinigameEnd(playerWon: false);
        }
        else if (CheckForTie())
        {
            ProcessMinigameEnd(playerWon: false, isTie: true);
        }
        else
        {
            // 6. If game continues, give control back to the player.
            isPlayerTurn = true;
            LogText("Your turn.", TextType.Narrative, useTypewriter: false);
        }
    }

    void ProcessMinigameEnd(bool playerWon, bool isTie = false)
    {
        string endMessage;
        if (isTie) { endMessage = "The board is full. It's a draw!"; }
        else if (playerWon) { endMessage = "You win! Congratulations!"; }
        else { endMessage = $"{minigameOpponentName} wins! Better luck next time."; }
        // Queue the two messages. They will now appear sequentially without glitches.
        LogText(endMessage, TextType.GameResponse);
        LogText("Press Enter to continue...", TextType.Narrative);
        currentGameState = GameState.MinigamePostlude;
    }

    private bool CheckForWin(int row, int col, int playerID)
    {
        // Check Horizontal
        int count = 0;
        for (int c = 0; c < C4_COLS; c++)
        {
            if (connectFourBoard[row, c] == playerID) count++;
            else count = 0;
            if (count >= 4) return true;
        }
        // Check Vertical
        count = 0;
        for (int r = 0; r < C4_ROWS; r++)
        {
            if (connectFourBoard[r, col] == playerID) count++;
            else count = 0;
            if (count >= 4) return true;
        }
        // Check Diagonal (down-right)
        count = 0;
        for (int r = row, c = col; r < C4_ROWS && c < C4_COLS; r++, c++)
        {
            if (connectFourBoard[r, c] == playerID) count++;
            else break;
        }
        for (int r = row - 1, c = col - 1; r >= 0 && c >= 0; r--, c--)
        {
            if (connectFourBoard[r, c] == playerID) count++;
            else break;
        }
        if (count >= 4) return true;
        // Check Diagonal (up-right)
        count = 0;
        for (int r = row, c = col; r >= 0 && c < C4_COLS; r--, c++)
        {
            if (connectFourBoard[r, c] == playerID) count++;
            else break;
        }
        for (int r = row + 1, c = col - 1; r < C4_ROWS && c >= 0; r++, c--)
        {
            if (connectFourBoard[r, c] == playerID) count++;
            else break;
        }
        if (count >= 4) return true;
        return false; // No win found
    }

    private bool CheckForTie()
    {
        // A tie occurs if the top row is completely full.
        for (int c = 0; c < C4_COLS; c++)
        {
            if (connectFourBoard[0, c] == 0) // If any spot in the top row is empty...
            {
                return false; // ...it's not a tie yet.
            }
        }
        return true; // The top row is full, so the board is full.
    }
    
    void EndConnectFour(string forfeitMessage = null)
    {
        currentGameState = GameState.Playing;
        if (!string.IsNullOrEmpty(forfeitMessage))
        {
            LogText(forfeitMessage, TextType.Narrative);
        }
        DisplayLocation(useTypewriter: false); 
    }

    void DisplayConnectFourBoard()
    {
        StringBuilder boardText = new StringBuilder();
        for (int row = 0; row < C4_ROWS; row++)
        {
            boardText.Append("|");
            for (int col = 0; col < C4_COLS; col++)
            {
                switch (connectFourBoard[row, col])
                {
                    case 0:
                        boardText.Append("<mspace=1.8em>.</mspace>");
                        break;
                    case 1:
                        boardText.Append($"<mspace=1.8em><color={playerInputColor}>O</color></mspace>");
                        break;
                    case 2:
                        boardText.Append($"<mspace=1.8em><color={enemyColor}>X</color></mspace>");
                        break;
                }
                boardText.Append("|");
            }
            boardText.Append("\n");
        }
        boardText.Append("-----------------------------\n");
        boardText.Append("|<mspace=1.8em>1</mspace>|<mspace=1.8em>2</mspace>|<mspace=1.8em>3</mspace>|<mspace=1.8em>4</mspace>|<mspace=1.8em>5</mspace>|<mspace=1.8em>6</mspace>|<mspace=1.8em>7</mspace>|\n");
        boardText.Append("-----------------------------");
        PrintToScreen(boardText.ToString(), useTypewriter: false);
    }
    #endregion

    #region Actions
    private void HandleCustomAction(CustomAction action, string nounPhrase)
    {
        if (string.IsNullOrEmpty(nounPhrase))
        {
            LogText(action.failureMessage);
            return;
        }
        // Find the target from the new runtime dictionary
        var target = FindByNoun(roomInteractablesState[currentLocation], i => i.noun, nounPhrase, out bool ambiguous);
        if (ambiguous) return;
        if (target != null && target.allowedCustomActions.Contains(action))
        {
            // Success! Process all the effects defined on the CustomAction asset.
            foreach (var effect in action.effects)
            {
                switch (effect.effectType)
                {
                    case ActionEffectType.LogMessage:
                        LogText(effect.stringParameter, TextType.GameResponse);
                        break;
                    case ActionEffectType.SetFlag:
                        worldFlags[effect.stringParameter] = true;
                        break;
                    case ActionEffectType.ChangeTargetState:
                        interactableStates[target] = effect.stringParameter;
                        break;
                    case ActionEffectType.GiveItemToPlayer:
                        TryGiveItem(effect.itemParameter);
                        break;
                    case ActionEffectType.TakeItemFromPlayer:
                        var heldInstance = effect.itemParameter != null
                            ? playerInventory.Find(inst => inst.blueprint == effect.itemParameter)
                            : null;
                        if (heldInstance != null)
                        {
                            playerInventory.Remove(heldInstance);
                            LogText($"You no longer have the {effect.itemParameter.itemName}.", TextType.GameResponse);
                        }
                        break;
                    case ActionEffectType.HealPlayer:
                        playerCurrentHealth = Mathf.Min(playerStats.maxHealth, playerCurrentHealth + effect.intParameter);
                        LogText($"You feel invigorated, restoring {effect.intParameter} health.", TextType.GameResponse);
                        break;
                    case ActionEffectType.DamagePlayer:
                        playerCurrentHealth -= effect.intParameter;
                        LogText($"You are hurt! You take {effect.intParameter} damage.", TextType.GameResponse);
                        break;
                    case ActionEffectType.MoveToLocation:
                        if (effect.locationParameter != null)
                        {
                            currentLocation = effect.locationParameter;
                            DisplayLocation();
                        }
                        break;
                    case ActionEffectType.DestroyTargetInteractable:
                        // Check if the key exists before trying to remove from the list
                        if (roomInteractablesState.ContainsKey(currentLocation))
                        {
                            roomInteractablesState[currentLocation].Remove(target);
                        }
                        LogText($"The {target.noun} is destroyed.", TextType.GameResponse);
                        break;
                    case ActionEffectType.PlaySound:
                        // This assumes you have a reference to a SoundManager instance.
                        // soundManager.sfxSource.PlayOneShot(effect.audioClipParameter);
                        break;
                }
            }
        }
        else
        {
            // The target doesn't allow this action.
            LogText(action.failureMessage);
        }
    }

    void HandleHelp()
    {
        StringBuilder helpText = new StringBuilder("Here are the commands you can use:");
        // Loop through all the Action assets you've assigned to the controller
        foreach (Action action in allActions)
        {
            helpText.Append("\n- " + action.keyword);
        }
        // Custom verbs are commands too.
        foreach (CustomAction action in allCustomActions)
        {
            helpText.Append("\n- " + action.keyword);
        }
        LogText(helpText.ToString(), TextType.Narrative);
    }

    void HandleEquip(string nounPhrase)
    {
        var itemToEquip = FindByNoun(playerInventory, inst => inst.blueprint.itemName, nounPhrase, out bool ambiguous);
        if (ambiguous) return;
        if (itemToEquip == null)
        {
            LogText("You don't have a " + nounPhrase + ".");
            return;
        }
        switch (itemToEquip.blueprint.itemType)
        {
            case ItemType.Weapon:
                // If a weapon is already equipped, unequip it first.
                if (equippedWeapon != null)
                {
                    playerInventory.Add(equippedWeapon);
                    LogText($"You unequip the {equippedWeapon.blueprint.itemName}.");
                }
                equippedWeapon = itemToEquip;
                playerInventory.Remove(itemToEquip);
                LogText("You equip the " + itemToEquip.blueprint.itemName + ".");
                RecalculatePlayerStats();
                break;
            case ItemType.Armor:
                // If armor is already equipped, unequip it first.
                if (equippedArmor != null)
                {
                    playerInventory.Add(equippedArmor);
                    LogText($"You unequip the {equippedArmor.blueprint.itemName}.");
                }
                equippedArmor = itemToEquip;
                playerInventory.Remove(itemToEquip);
                LogText("You equip the " + itemToEquip.blueprint.itemName + ".");
                RecalculatePlayerStats();
                break;
            default:
                LogText("You can't equip a " + nounPhrase + ".");
                break;
        }
    }

    void HandleUnequip(string nounPhrase)
    {
        var equipped = new List<ItemInstance>();
        if (equippedWeapon != null) equipped.Add(equippedWeapon);
        if (equippedArmor != null) equipped.Add(equippedArmor);
        var itemToUnequip = FindByNoun(equipped, inst => inst.blueprint.itemName, nounPhrase, out bool ambiguous);
        if (ambiguous) return;
        if (itemToUnequip == null)
        {
            LogText("You don't have a " + nounPhrase + " equipped.");
            return;
        }
        playerInventory.Add(itemToUnequip);
        LogText("You unequip the " + itemToUnequip.blueprint.itemName + ".");
        if (itemToUnequip == equippedWeapon) equippedWeapon = null;
        else equippedArmor = null;
        RecalculatePlayerStats();
    }

    void HandleEquipment()
    {
        StringBuilder equipmentText = new StringBuilder("You are wearing:");
        equipmentText.Append("\nWeapon: " + (equippedWeapon != null ? equippedWeapon.blueprint.itemName : "none"));
        equipmentText.Append("\nArmor: " + (equippedArmor != null ? equippedArmor.blueprint.itemName : "none"));
        LogText(equipmentText.ToString(), TextType.Narrative);
    }

    void HandleBalance()
    {
        LogText($"You have {playerStats.currency} coins.", TextType.GameResponse);
    }

    void HandleBuy(string nounPhrase)
    {
        if (!currentLocation.isShop)
        {
            LogText("There isn’t a shop here.", TextType.GameResponse);
            return;
        }
        // Use the runtime inventory for the transaction.
        var shopStock = runtimeShopInventories[currentLocation];
        var instance = FindByNoun(shopStock, inst => inst.blueprint.itemName, nounPhrase, out bool ambiguous);
        if (ambiguous) return;
        if (instance == null)
        {
            LogText($"The shop doesn’t sell a {nounPhrase}.", TextType.GameResponse);
            return;
        }
        Item item = instance.blueprint;
        if (playerStats.currency < item.buyPrice)
        {
            LogText("You can’t afford that.", TextType.GameResponse);
            return;
        }
        // Enforce the item's stack cap on player purchases.
        if (item.isStackable && item.maxStackSize > 0 && CountInInventory(item) >= item.maxStackSize)
        {
            LogText($"You can't carry any more {item.itemName}.", TextType.GameResponse);
            return;
        }
        shopStock.Remove(instance); // Modify the runtime copy, NOT the asset.
        playerStats.currency -= item.buyPrice;
        playerInventory.Add(instance);
        LogText($"You spend {item.buyPrice} coins and buy a {item.itemName}. You now have {playerStats.currency} coins.", TextType.GameResponse);
    }

    void HandleSell(string nounPhrase)
    {
        if (!currentLocation.isShop)
        {
            LogText("There isn’t a shop here.", TextType.GameResponse);
            return;
        }
        var instance = FindByNoun(playerInventory, inst => inst.blueprint.itemName, nounPhrase, out bool ambiguous);
        if (ambiguous) return;
        if (instance == null)
        {
            LogText($"You don’t have a {nounPhrase} to sell.", TextType.GameResponse);
            return;
        }
        // Add the sold item to the runtime inventory copy.
        runtimeShopInventories[currentLocation].Add(instance);
        playerInventory.Remove(instance);
        playerStats.currency += instance.blueprint.sellPrice;
        LogText($"You sell the {instance.blueprint.itemName} for {instance.blueprint.sellPrice} coins. You now have {playerStats.currency} coins.", TextType.GameResponse);
    }

    void HandleSave()
    {
        SaveData saveData = new SaveData();
        // --- 1. Player & World State ---
        // Save the entire PlayerStats object and the player's current health.
        saveData.playerStats = this.playerStats;
        saveData.playerCurrentHealth = this.playerCurrentHealth;
        saveData.playerName = this.playerName;
        saveData.playerDrunkenness = this.playerDrunkenness;
        saveData.currentLocationName = currentLocation != null ? currentLocation.name : "";
        // --- 1b. Quests ---
        saveData.activeQuests = activeQuests
            .Where(aq => aq?.quest != null)
            .Select(aq => new ActiveQuestSaveState
            {
                questName = aq.quest.name,
                objectivesCompleted = new List<bool>(aq.objectivesCompleted)
            })
            .ToList();
        saveData.completedQuestNames = completedQuests.Where(q => q != null).Select(q => q.name).ToList();
        // --- 1c. Active Status Effects ---
        saveData.activeStatusEffects = activeStatusEffects
            .Where(ae => ae?.effect != null)
            .Select(ae => new StatusEffectSaveState
            {
                effectName = ae.effect.name,
                remainingTime = ae.RemainingTime
            })
            .ToList();
        // --- 2. Learned Skills ---
        // Convert the list of Skill ScriptableObjects to a list of their names for serialization.
        saveData.learnedSkillNames = learnedSkills.Select(s => s.name).ToList();
        // --- 3. Equipment ---
        // Save the names of the equipped weapon and armor.
        saveData.equippedWeaponName = equippedWeapon != null ? equippedWeapon.blueprint.name : null;
        saveData.equippedArmorName = equippedArmor != null ? equippedArmor.blueprint.name : null;
        // --- 4. Inventory ---
        // Convert the player's inventory to a list of item names.
        saveData.playerInventoryItemNames = playerInventory
            .Where(inst => inst?.blueprint != null)
            .Select(inst => inst.blueprint.name)
            .ToList();
        // --- 5. Room Items State ---
        // Loop through every location and save the items currently inside it.
        saveData.roomItemsState.Clear();
        saveData.roomInteractablesState.Clear();
        if (roomInteractablesState != null)
        {
            foreach (var kvp in roomInteractablesState)
            {
                if (kvp.Key == null || kvp.Value == null) continue;
                var room = new RoomInteractables
                {
                    locationName = kvp.Key.name,
                    interactableNames = kvp.Value.Where(i => i != null).Select(i => i.name).ToList()
                };
                saveData.roomInteractablesState.Add(room);
            }
        }
        if (roomItemsState != null)
        {
            foreach (var kvp in roomItemsState)
            {
                if (kvp.Key == null || kvp.Value == null) continue;
                var room = new RoomItems
                {
                    locationName = kvp.Key.name,
                    itemNames = kvp.Value.Where(inst => inst?.blueprint != null).Select(inst => inst.blueprint.name).ToList()
                };
                saveData.roomItemsState.Add(room);
            }
        }
        // --- 6. Exit States (Locked & Visible) ---
        // Loop through all exits and save their current locked/unlocked and visible/hidden states.
        saveData.exitLockedState.Clear();
        if (exitLockedState != null)
        {
            foreach (var kvp in exitLockedState)
            {
                if (kvp.Key == null) continue;
                var loc = FindLocationOfExit(kvp.Key);
                if (loc == null) continue;
                saveData.exitLockedState.Add(new ExitState
                {
                    locationName = loc.name,
                    exitDirection = kvp.Key.direction,
                    state = kvp.Value
                });
            }
        }
        saveData.exitVisibilityState.Clear();
        if (exitVisibilityState != null)
        {
            foreach (var kvp in exitVisibilityState)
            {
                if (kvp.Key == null) continue;
                var loc = FindLocationOfExit(kvp.Key);
                if (loc == null) continue;
                saveData.exitVisibilityState.Add(new ExitState
                {
                    locationName = loc.name,
                    exitDirection = kvp.Key.direction,
                    state = kvp.Value
                });
            }
        }
        // --- 7. Interactable States ---
        // Save the current state string for every interactable object in the
        // game, identified by its home location + asset name.
        saveData.interactableStates.Clear();
        if (interactableStates != null)
        {
            foreach (var kvp in interactableStates)
            {
                if (kvp.Key == null) continue;
                var home = world.FindLocationOfInteractable(kvp.Key);
                if (home == null) continue;
                saveData.interactableStates.Add(new InteractableState
                {
                    locationName = home.name,
                    interactableName = kvp.Key.name,
                    state = kvp.Value ?? ""
                });
            }
        }
        // --- 8. Enemy States ---
        // Save each enemy instance, including its location and current health.
        saveData.roomEnemiesState.Clear();
        if (roomEnemiesState != null)
        {
            foreach (var kvp in roomEnemiesState)
            {
                if (kvp.Key == null || kvp.Value == null) continue;
                foreach (var enemyInst in kvp.Value)
                {
                    if (enemyInst?.enemyBlueprint == null) continue;
                    saveData.roomEnemiesState.Add(new EnemySaveState
                    {
                        locationName = kvp.Key.name,
                        enemyBlueprintName = enemyInst.enemyBlueprint.name,
                        currentHealth = enemyInst.currentHealth
                    });
                }
            }
        }
        // --- 9. Character States ---
        // Save which characters are present in each location.
        saveData.roomCharactersState.Clear();
        if (roomCharactersState != null)
        {
            foreach (var kvp in roomCharactersState)
            {
                if (kvp.Key == null || kvp.Value == null) continue;
                var room = new RoomCharacters
                {
                    locationName = kvp.Key.name,
                    characterNames = kvp.Value.Select(c => c.name).ToList()
                };
                saveData.roomCharactersState.Add(room);
            }
        }
        // --- 10. Shop Inventories ---
        // Save the current inventory of all shop locations.
        saveData.shopStates.Clear();
        foreach (var kvp in runtimeShopInventories)
        {
            var shopState = new ShopSaveState
            {
                locationName = kvp.Key.name,
                shopItemNames = kvp.Value.Where(inst => inst?.blueprint != null).Select(inst => inst.blueprint.name).ToList()
            };
            saveData.shopStates.Add(shopState);
        }
        // --- 11. World Flags ---
        // Save a list of all flags that are currently set to 'true'.
        saveData.trueFlags = worldFlags?
            .Where(f => f.Value)
            .Select(f => f.Key)
            .ToList()
          ?? new List<string>();
        // --- Finalize: hand the snapshot to the save system to persist. ---
        saveSystem.Write(saveData);
        LogText("Game saved.");
    }

    public void HandleLoad()
    {
        SaveData saveData = saveSystem.Read();
        if (saveData == null)
        {
            LogText("No save game found.");
            return;
        }
        InitializeGame();
        // --- 1. Load Player & World State ---
        this.playerStats = saveData.playerStats;
        this.playerCurrentHealth = saveData.playerCurrentHealth;
        this.playerName = saveData.playerName;
        this.playerDrunkenness = saveData.playerDrunkenness;
        currentLocation = FindLocationByName(saveData.currentLocationName);
        // A renamed or deleted location asset must not strand the player in
        // a null room — fall back to the main start location.
        if (currentLocation == null)
        {
            Debug.LogWarning($"Saved location '{saveData.currentLocationName}' was not found. Falling back to the main start location.");
            currentLocation = mainGameStartLocation;
        }
        // --- 1b. Load Quests ---
        activeQuests.Clear();
        completedQuests.Clear();
        foreach (var questSave in saveData.activeQuests)
        {
            Quest quest = world.FindQuestByName(questSave.questName);
            if (quest == null) continue;
            var activeQuest = new ActiveQuest(quest);
            // Copy saved objective progress; guard against objective-count
            // changes between the save and the current quest asset.
            for (int i = 0; i < activeQuest.objectivesCompleted.Count && i < questSave.objectivesCompleted.Count; i++)
            {
                activeQuest.objectivesCompleted[i] = questSave.objectivesCompleted[i];
            }
            activeQuests.Add(activeQuest);
        }
        foreach (var questName in saveData.completedQuestNames)
        {
            Quest quest = world.FindQuestByName(questName);
            if (quest != null) completedQuests.Add(quest);
        }
        // --- 1c. Load Active Status Effects ---
        activeStatusEffects.Clear();
        foreach (var effectSave in saveData.activeStatusEffects)
        {
            StatusEffect effect = world.FindStatusEffectByName(effectSave.effectName);
            if (effect != null && effectSave.remainingTime > 0f)
            {
                activeStatusEffects.Add(new ActiveStatusEffect(effect, effectSave.remainingTime));
            }
        }
        // --- 2. Load Learned Skills ---
        learnedSkills.Clear();
        foreach (var skillName in saveData.learnedSkillNames)
        {
            Skill skill = allSkills.FirstOrDefault(s => s.name == skillName);
            if (skill != null)
            {
                learnedSkills.Add(skill);
            }
        }
        // --- 3. Load Equipment & Inventory ---
        Item weaponBlueprint = FindItemByName(saveData.equippedWeaponName);
        Item armorBlueprint = FindItemByName(saveData.equippedArmorName);
        equippedWeapon = weaponBlueprint != null ? new ItemInstance(weaponBlueprint) : null;
        equippedArmor = armorBlueprint != null ? new ItemInstance(armorBlueprint) : null;
        playerInventory = saveData.playerInventoryItemNames
            .Select(name => FindItemByName(name))
            .Where(item => item != null)
            .Select(item => new ItemInstance(item))
            .ToList();
        RecalculatePlayerStats();
        // --- 4. Load Room Items State ---
        foreach (RoomItems roomData in saveData.roomItemsState)
        {
            Location location = FindLocationByName(roomData.locationName);
            if (location != null)
            {
                List<ItemInstance> itemsInRoom = roomData.itemNames
                    .Select(name => FindItemByName(name))
                    .Where(item => item != null)
                    .Select(item => new ItemInstance(item))
                    .ToList();
                roomItemsState[location] = itemsInRoom;
            }
        }
        // --- 5. Load Exit States ---
        foreach (ExitState es in saveData.exitLockedState)
        {
            Location loc = FindLocationByName(es.locationName);
            if (loc != null)
            {
                Exit exit = FindExit(loc, es.exitDirection);
                if (exit != null)
                {
                    exitLockedState[exit] = es.state;
                }
            }
        }
        foreach (ExitState es in saveData.exitVisibilityState)
        {
            Location loc = FindLocationByName(es.locationName);
            if (loc != null)
            {
                Exit exit = FindExit(loc, es.exitDirection);
                if (exit != null)
                {
                    exitVisibilityState[exit] = es.state;
                }
            }
        }
        // --- 6. Load Interactable States ---
        foreach (InteractableState interactableData in saveData.interactableStates)
        {
            Location home = FindLocationByName(interactableData.locationName);
            Interactable interactable = world.FindInteractableInLocation(home, interactableData.interactableName);
            if (interactable != null)
            {
                interactableStates[interactable] = interactableData.state;
            }
        }
        // --- 7. Load Enemy States ---
        foreach (var entry in roomEnemiesState) { entry.Value.Clear(); }
        foreach (EnemySaveState enemySave in saveData.roomEnemiesState)
        {
            Location location = FindLocationByName(enemySave.locationName);
            if (location != null)
            {
                Enemy enemyBlueprint = FindEnemyBlueprintByName(enemySave.enemyBlueprintName);
                if (enemyBlueprint != null)
                {
                    EnemyInstance instance = new EnemyInstance(enemyBlueprint) { currentHealth = enemySave.currentHealth };
                    roomEnemiesState[location].Add(instance);
                }
            }
        }
        // --- 8. Load Character States ---
        foreach (var entry in roomCharactersState) { entry.Value.Clear(); }
        foreach (RoomCharacters roomData in saveData.roomCharactersState)
        {
            Location location = FindLocationByName(roomData.locationName);
            if (location != null)
            {
                List<Character> charactersInRoom = roomData.characterNames
                    .Select(name => FindCharacterByName(name))
                    .Where(c => c != null)
                    .ToList();
                roomCharactersState[location] = charactersInRoom;
            }
        }
        // --- 9. Load Shop Inventories ---
        foreach (var shopState in saveData.shopStates)
        {
            var loc = FindLocationByName(shopState.locationName);
            if (loc != null && loc.isShop)
            {
                List<ItemInstance> runtimeItems = shopState.shopItemNames
                    .Select(name => FindItemByName(name))
                    .Where(item => item != null)
                    .Select(item => new ItemInstance(item))
                    .ToList();
                // Overwrite the runtime inventory with the saved data.
                runtimeShopInventories[loc] = runtimeItems;
            }
        }
        // --- 10. Load World Flags ---
        worldFlags.Clear();
        foreach (string flagName in saveData.trueFlags)
        {
            worldFlags[flagName] = true;
        }
        foreach (var roomData in saveData.roomInteractablesState)
        {
            Location location = FindLocationByName(roomData.locationName);
            if (location != null)
            {
                List<Interactable> runtimeInteractables = roomData.interactableNames
                    .Select(name => world.FindInteractableInLocation(location, name))
                    .Where(i => i != null)
                    .ToList();

                roomInteractablesState[location] = runtimeInteractables;
            }
        }
        // --- Finalize ---
        currentGameState = GameState.Playing;
        DisplayLocation(useTypewriter: false);
        LogText("Game loaded. Welcome back.");
    }

    void HandleLook(string nounPhrase)
    {
        if (string.IsNullOrEmpty(nounPhrase))
        {
            DisplayLocation(useTypewriter: false);
            return;
        }
        bool ambiguous;
        // Check room items
        var itemInRoom = FindByNoun(roomItemsState[currentLocation], inst => inst.blueprint.itemName, nounPhrase, out ambiguous);
        if (ambiguous) return;
        if (itemInRoom != null)
        {
            LogText(itemInRoom.blueprint.description);
            return;
        }
        // Check interactables
        var interactableInRoom = FindByNoun(roomInteractablesState[currentLocation], i => i.noun, nounPhrase, out ambiguous);
        if (ambiguous) return;
        if (interactableInRoom != null)
        {
            LogText(interactableInRoom.detailedDescription);
            return;
        }

        // Check characters
        var characterInRoom = FindByNoun(roomCharactersState[currentLocation], c => c.characterName, nounPhrase, out ambiguous);
        if (ambiguous) return;
        if (characterInRoom != null)
        {
            LogText(characterInRoom.detailedDescription);
            return;
        }
        // Check enemies
        var enemyInRoom = FindByNoun(roomEnemiesState[currentLocation], e => e.enemyBlueprint.enemyName, nounPhrase, out ambiguous);
        if (ambiguous) return;
        if (enemyInRoom != null)
        {
            string descriptionWithHealth = enemyInRoom.enemyBlueprint.detailedDescription +
                                           $"\nIt has {enemyInRoom.currentHealth}/{enemyInRoom.enemyBlueprint.maxHealth} health.";
            LogText(descriptionWithHealth);
            return;
        }
        // Check inventory
        var itemInInventory = FindByNoun(playerInventory, inst => inst.blueprint.itemName, nounPhrase, out ambiguous);
        if (ambiguous) return;
        if (itemInInventory != null)
        {
            LogText(itemInInventory.blueprint.description);
            return;
        }
        LogText("There is no " + nounPhrase + " to look at.");
    }

    void HandleTake(string nounPhrase)
    {
        var instanceToTake = FindByNoun(roomItemsState[currentLocation], inst => inst.blueprint.itemName, nounPhrase, out bool ambiguous);
        if (ambiguous) return;
        if (instanceToTake != null)
        {
            Item itemToTake = instanceToTake.blueprint;
            if (itemToTake.itemType == ItemType.Currency)
            {
                // The value of the currency item is its sell price.
                int value = itemToTake.sellPrice;
                // Add the item's value to the player's total currency.
                playerStats.currency += value;
                // Remove the currency item from the room.
                roomItemsState[currentLocation].Remove(instanceToTake);
                // Fire the onItemTaken event so the sound can play.
                onItemTaken.Invoke(itemToTake);
                // Log a specific message to the player.
                LogText($"You take the {itemToTake.itemName} and find {value} coins. You now have {playerStats.currency} coins.", TextType.GameResponse);
                // Instantly refresh the room to show the item is gone.
                DisplayLocation(useTypewriter: false);
                // Stop the method here so we don't add it to the inventory.
                return;
            }
            // Enforce the item's stack cap when the player picks it up.
            if (itemToTake.isStackable && itemToTake.maxStackSize > 0 && CountInInventory(itemToTake) >= itemToTake.maxStackSize)
            {
                LogText($"You can't carry any more {itemToTake.itemName}.");
                return;
            }
            roomItemsState[currentLocation].Remove(instanceToTake);
            playerInventory.Add(instanceToTake);
            DisplayLocation(useTypewriter: false);
            onItemTaken.Invoke(itemToTake);
            LogText("You take the " + itemToTake.itemName + ".");
        }
        else
        {
            LogText("There is no " + nounPhrase + " here.");
        }
    }

    void HandleUse(string itemNounPhrase, string targetNounPhrase)
    {
        // Find the item the player is trying to use from their inventory.
        var itemInInventory = FindByNoun(playerInventory, inst => inst.blueprint.itemName, itemNounPhrase, out bool itemAmbiguous);
        if (itemAmbiguous) return;
        if (itemInInventory == null)
        {
            LogText("You don't have a " + itemNounPhrase + ".");
            return;
        }
        Item usedItem = itemInInventory.blueprint;
        // --- Case 1: Using a CONSUMABLE item without a target (e.g., "use potion") ---
        if (string.IsNullOrEmpty(targetNounPhrase) && usedItem.itemType == ItemType.Consumable)
        {
            bool itemWasConsumed = false;
            // Health restoration
            if (usedItem.healthToRestore > 0)
            {
                playerCurrentHealth = Mathf.Min(playerStats.maxHealth, playerCurrentHealth + usedItem.healthToRestore);
                LogText($"You use the {usedItem.itemName} and restore {usedItem.healthToRestore} health. You now have {playerCurrentHealth}/{playerStats.maxHealth} health.");
                itemWasConsumed = true;
            }
            // Drunkenness effect
            if (engineSettings.useDrunkennessSystem && usedItem.drunkennessValue > 0)
            {
                playerDrunkenness += usedItem.drunkennessValue;
                playerDrunkenness = Mathf.Clamp(playerDrunkenness, 0f, 1f);
                LogText("You feel a bit dizzy...");
                itemWasConsumed = true;
            }
            // Status effect application
            if (engineSettings.useStatusEffectSystem && usedItem.effectToApplyOnUse != null)
            {
                if (Random.value <= usedItem.effectToApplyOnUse.chanceToApply)
                {
                    ActiveStatusEffect newEffect = new ActiveStatusEffect(usedItem.effectToApplyOnUse);
                    activeStatusEffects.Add(newEffect);
                    LogText(newEffect.effect.applicationMessage);
                }
                itemWasConsumed = true;
            }
            if (itemWasConsumed)
            {
                playerInventory.Remove(itemInInventory);
                return;
            }
        }
        // --- Case 2: Using an item ON an INTERACTABLE target (e.g., "use handle on toilet") ---
        if (!string.IsNullOrEmpty(targetNounPhrase))
        {
            var target = FindByNoun(roomInteractablesState[currentLocation], i => i.noun, targetNounPhrase, out bool targetAmbiguous);
            if (targetAmbiguous) return;
            if (target == null)
            {
                LogText("There is no " + targetNounPhrase + " here to use that on.");
                return;
            }
            string currentState = interactableStates[target];
            foreach (Interaction interaction in target.interactions)
            {
                if (interaction.requiredState == currentState && interaction.requiredItem == usedItem)
                {
                    ProcessInteractionEffects(interaction, target, itemInInventory);
                    return;
                }
            }
            LogText("That doesn't seem to do anything.");
            return;
        }
        // --- Case 3 (THE FIX): Using a KEY on a LOCKED EXIT without a target ---
        if (string.IsNullOrEmpty(targetNounPhrase))
        {
            foreach (Exit exit in currentLocation.exits)
            {
                // Check if this exit is locked AND if the item used is the correct key.
                if (exitLockedState.ContainsKey(exit) && exitLockedState[exit] && exit.keyToUnlock == usedItem)
                {
                    exitLockedState[exit] = false; // Unlock the exit
                    LogText($"You use the {usedItem.itemName}. You hear a click as the {exit.direction} door unlocks.", TextType.GameResponse);
                    return; // Stop the function here.
                }
            }
        }
        // --- Fallback Case: If none of the above worked ---
        LogText("You can't seem to use the " + itemNounPhrase + " here by itself.");
    }

    private void HandleInteract(string verb, string nounPhrase)
    {
        var target = FindByNoun(roomInteractablesState[currentLocation], i => i.noun, nounPhrase, out bool ambiguous);
        if (ambiguous) return;
        if (target == null)
        {
            LogText("There is no " + nounPhrase + " here to interact with.");
            return;
        }
        string currentState = interactableStates[target];
        // Find an interaction that matches the VERB and the current STATE.
        foreach (Interaction interaction in target.interactions)
        {
            // The interaction must match the verb, the state, and NOT require an item.
            if (interaction.interactionVerb.ToLower() == verb.ToLower() &&
                interaction.requiredState == currentState &&
                interaction.requiredItem == null)
            {
                ProcessInteractionEffects(interaction, target, null);
                return;
            }
        }
        // If no specific interaction was found for that verb.
        LogText("That doesn't seem to work.");
    }

    private void ProcessInteractionEffects(Interaction interaction, Interactable sourceInteractable, ItemInstance itemUsed = null)
    {
        foreach (var effect in interaction.effects)
        {
            switch (effect.effectType)
            {
                case InteractionEffectType.LogMessage:
                    LogText(effect.stringParameter, TextType.GameResponse);
                    break;
                case InteractionEffectType.ChangeState:
                    interactableStates[sourceInteractable] = effect.newStateParameter;
                    break;
                case InteractionEffectType.SetFlag:
                    worldFlags[effect.flagParameter] = true;
                    break;
                case InteractionEffectType.RevealExit:
                    Exit exitToReveal = currentLocation.exits.FirstOrDefault(e => e.direction.ToLower() == effect.stringParameter.ToLower());
                    if (exitToReveal != null)
                    {
                        exitVisibilityState[exitToReveal] = true;
                        LogText($"A new exit has opened to the {effect.stringParameter}.", TextType.GameResponse);
                    }
                    break;
                case InteractionEffectType.SpawnItemInRoom:
                    if (effect.itemParameter != null)
                    {
                        roomItemsState[currentLocation].Add(new ItemInstance(effect.itemParameter));
                        LogText($"A {effect.itemParameter.itemName} is revealed!", TextType.GameResponse);
                    }
                    break;
                case InteractionEffectType.PlaySound:
                    // soundManager.PlayOneShot(effect.audioClipParameter);
                    break;
                // --- ADD THIS NEW CASE ---
                case InteractionEffectType.ConsumeUsedItem:
                    if (itemUsed != null && playerInventory.Contains(itemUsed))
                    {
                        playerInventory.Remove(itemUsed);
                        LogText($"(You used the {itemUsed.blueprint.itemName})", TextType.GameResponse);
                    }
                    break;
            }
        }
        // Refresh the room description to reflect any changes.
        DisplayLocation(useTypewriter: false);
    }

    private void HandleQuests()
    {
        // Use a StringBuilder for efficient string construction.
        StringBuilder sb = new StringBuilder();
        sb.Append("--- Active Quests ---\n");

        if (activeQuests.Count == 0)
        {
            sb.Append("\nYou have no active quests.");
        }
        else
        {
            foreach (ActiveQuest activeQuest in activeQuests)
            {
                // Quest Name
                sb.Append($"\n<b>{activeQuest.quest.questName}</b>");
                // Quest Description
                sb.Append($"\n<i>{activeQuest.quest.questDescription}</i>");
                // Objectives
                for (int i = 0; i < activeQuest.quest.objectives.Count; i++)
                {
                    // Show [x] for completed, [ ] for incomplete
                    string status = activeQuest.objectivesCompleted[i] ? "[x]" : "[ ]";
                    sb.Append($"\n{status} {activeQuest.quest.objectives[i]}");
                }
                sb.Append("\n");
            }
        }
        // Log the completed string to the screen as a narrative message.
        LogText(sb.ToString(), TextType.Narrative);
    }

    private void HandleStatus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("--- Player Status ---");
        sb.AppendLine();
        sb.AppendLine($"Health: {playerCurrentHealth} / {playerStats.maxHealth}");
        sb.AppendLine();
        sb.AppendLine("<b>Active Effects:</b>");

        if (activeStatusEffects.Count == 0)
        {
            sb.AppendLine("None");
        }
        else
        {
            foreach (var ae in activeStatusEffects)
            {
                // Show seconds remaining (one decimal place)
                sb.AppendLine(
                    $"- {ae.effect.effectName} ({ae.RemainingTime:F1}s remaining)"
                );
            }
        }

        LogText(sb.ToString(), TextType.Narrative);
    }

    private void HandleChar()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("--- Character Sheet ---\n");
        if (engineSettings.useLevelingSystem)
        {
            sb.Append($"\nLevel: {playerStats.level}");
            sb.Append($"\nXP: {playerStats.currentXp} / {playerStats.xpToNextLevel}");
            sb.Append($"\nSkill Points Available: {playerStats.skillPoints}");
        }
        sb.Append($"\nCoins: {playerStats.currency}");
        if (engineSettings.usePrimaryAttributes)
        {
            sb.Append("\n\n--- Primary Attributes ---");
            sb.Append($"\nStrength: {playerStats.strength}");
            sb.Append($"\nAgility: {playerStats.agility}");
            sb.Append($"\nStamina: {playerStats.stamina}");
            sb.Append($"\nIntellect: {playerStats.intellect}");
        }
        sb.Append("\n\n--- Combat Stats ---");
        sb.Append($"\nHealth: {playerCurrentHealth} / {playerStats.maxHealth}");
        sb.Append($"\nMana: {playerStats.currentMana} / {playerStats.maxMana}");
        sb.Append($"\nAttack Power: {playerStats.baseAttack}");
        sb.Append($"\nHit Chance: {playerStats.hitChance:P1}");
        sb.Append($"\nDodge Chance: {playerStats.dodgeChance:P1}");
        LogText(sb.ToString(), TextType.Narrative);
    }

    private void HandleSkills(string nounPhrase)
    {
        // If the player types "skills" with no arguments, show their character sheet.
        if (string.IsNullOrEmpty(nounPhrase))
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"\n\n<color=yellow>Skill Points Available: {playerStats.skillPoints}</color>\n");
            sb.Append("\n<b>Learned Skills:</b>");
            if (learnedSkills.Count == 0)
            {
                sb.Append("\nNone");
            }
            else
            {
                foreach (var skill in learnedSkills)
                {
                    sb.Append($"\n- {skill.skillName}: {skill.skillDescription}");
                }
            }
            LogText(sb.ToString(), TextType.Narrative);
            return;
        }
        // Handle sub-commands like "skills list" or "skills learn [skill name]"
        string[] words = nounPhrase.Split(' ');
        string subCommand = words[0];
        if (subCommand == "list")
        {
            StringBuilder sb = new StringBuilder("--- Available Skills ---\n");
            foreach (var skill in allSkills)
            {
                // Don't list skills the player already has
                if (learnedSkills.Contains(skill)) continue;
                sb.Append($"\n<b>{skill.skillName}</b> (Cost: {skill.skillPointCost}, Lvl Req: {skill.requiredLevel})");
                sb.Append($"\n<i>{skill.skillDescription}</i>\n");
            }
            LogText(sb.ToString(), TextType.Narrative);
        }
        else if (subCommand == "learn")
        {
            string skillNameToLearn = string.Join(" ", words.Skip(1));
            HandleLearnSkill(skillNameToLearn);
        }
    }

    void HandleDrop(string nounPhrase)
    {
        var itemToDrop = FindByNoun(playerInventory, inst => inst.blueprint.itemName, nounPhrase, out bool ambiguous);
        if (ambiguous) return;
        if (itemToDrop != null)
        {
            playerInventory.Remove(itemToDrop);
            roomItemsState[currentLocation].Add(itemToDrop);
            DisplayLocation(useTypewriter: false);
            onItemDropped.Invoke(itemToDrop.blueprint);
            LogText("You drop the " + itemToDrop.blueprint.itemName + ".");
        }
        else
        {
            LogText("You don't have a " + nounPhrase + ".");
        }
    }

    // Returns how many copies of the item blueprint the player is holding.
    private int CountInInventory(Item item) => playerInventory.Count(inst => inst.blueprint == item);

    // Adds an item to the player's inventory unless its stack cap is already
    // reached, and announces the result. Every grant path (dialogue gifts,
    // custom actions, quest rewards) goes through here so the cap that 'take'
    // and 'buy' enforce can't be bypassed. Returns whether the item was given.
    private bool TryGiveItem(Item item)
    {
        if (item == null) return false;
        if (item.isStackable && item.maxStackSize > 0 && CountInInventory(item) >= item.maxStackSize)
        {
            LogText($"You can't carry any more {item.itemName}.", TextType.GameResponse);
            return false;
        }
        playerInventory.Add(new ItemInstance(item));
        LogText($"You receive the <color={keywordColor}>{item.itemName}</color>.", TextType.GameResponse);
        return true;
    }

    // Groups identical stackable items and appends " xN" when count > 1.
    // Non-stackable items (isStackable == false) are listed individually.
    // Each line is produced by the given formatter, which receives the item
    // blueprint and the count represented by that line. First-seen ordering is
    // preserved by tracking keys in a list alongside the count dictionary.
    private List<string> BuildGroupedItemLines(IEnumerable<ItemInstance> items, System.Func<Item, int, string> lineFormatter)
    {
        // 'order' records one entry per output line, in first-seen order.
        // Stackable items appear once here (their count accumulates in 'counts');
        // non-stackable items get a fresh entry each time so they list individually.
        var order = new List<Item>();
        var counts = new Dictionary<Item, int>();
        foreach (var instance in items)
        {
            var item = instance?.blueprint;
            if (item == null) continue;
            if (item.isStackable)
            {
                if (counts.ContainsKey(item))
                {
                    counts[item]++;
                }
                else
                {
                    counts[item] = 1;
                    order.Add(item);
                }
            }
            else
            {
                // Non-stackable items are always listed individually.
                order.Add(item);
            }
        }
        var lines = new List<string>();
        foreach (var item in order)
        {
            int count = item.isStackable ? counts[item] : 1;
            lines.Add(lineFormatter(item, count));
        }
        return lines;
    }

    void HandleInventory()
    {
        // First check is still valid.
        if (playerInventory == null || playerInventory.Count == 0)
        {
            LogText("You are not carrying anything.");
            return;
        }
        List<string> lines = BuildGroupedItemLines(playerInventory,
            (item, count) => "- " + item.itemName + (count > 1 ? " x" + count : ""));

        // If our inventory only contained null items, we should still say we have nothing.
        if (lines.Count == 0)
        {
            LogText("You are not carrying anything.");
        }
        else
        {
            StringBuilder inventoryText = new StringBuilder("You are carrying:");
            foreach (string line in lines)
            {
                inventoryText.Append("\n" + line);
            }
            LogText(inventoryText.ToString(), TextType.Narrative);
        }
    }
    #endregion

    private void HandleLearnSkill(string skillName)
    {
        // Find the skill from the master list.
        var skillToLearn = allSkills.FirstOrDefault(s => s.skillName.ToLower() == skillName.ToLower());
        if (skillToLearn == null)
        {
            LogText($"There is no skill called '{skillName}'.", TextType.GameResponse);
            return;
        }
        // Check all conditions for learning the skill.
        if (learnedSkills.Contains(skillToLearn))
        {
            LogText("You have already learned that skill.", TextType.GameResponse);
            return;
        }
        if (playerStats.level < skillToLearn.requiredLevel)
        {
            LogText($"You must be level {skillToLearn.requiredLevel} to learn that skill.", TextType.GameResponse);
            return;
        }
        if (playerStats.skillPoints < skillToLearn.skillPointCost)
        {
            LogText("You do not have enough skill points.", TextType.GameResponse);
            return;
        }
        // All checks passed. Learn the skill.
        playerStats.skillPoints -= skillToLearn.skillPointCost;
        learnedSkills.Add(skillToLearn);
        RecalculatePlayerStats();
        LogText($"You have learned {skillToLearn.skillName}!", TextType.GameResponse);
    }

    private void RecalculatePlayerStats()
    {
        // Flat attack from equipment applies in both stat modes. (defenseBonus
        // is intentionally NOT baked in here — armor mitigation is applied at
        // combat time in PerformStandardAttack.)
        int equipmentAttack = (equippedWeapon != null ? equippedWeapon.blueprint.attackBonus : 0)
                            + (equippedArmor != null ? equippedArmor.blueprint.attackBonus : 0);
        // If we are NOT using the primary attribute system, calculate simple stats and exit.
        if (!engineSettings.usePrimaryAttributes)
        {
            // Simple, direct stats for a less complex game.
            playerStats.maxHealth = engineSettings.simpleMaxHealth;
            playerStats.baseAttack = engineSettings.simpleBaseAttack + equipmentAttack;
            playerStats.hitChance = engineSettings.simpleHitChance;
            playerStats.dodgeChance = engineSettings.simpleDodgeChance;
            // Ensure current health doesn't exceed new max health
            playerCurrentHealth = Mathf.Min(playerCurrentHealth, playerStats.maxHealth);
            return; // Stop the function here.
        }
        // --- If we ARE using primary attributes, run the full calculation ---
        // 1. Initialize bonus variables to zero.
        int strength_bonus = 0;
        int agility_bonus = 0;
        int stamina_bonus = 0;
        int intellect_bonus = 0;
        // 2. Add attribute bonuses from equipped items.
        foreach (var equipped in new[] { equippedWeapon, equippedArmor })
        {
            if (equipped == null) continue;
            strength_bonus += equipped.blueprint.strengthBonus;
            agility_bonus += equipped.blueprint.agilityBonus;
            stamina_bonus += equipped.blueprint.staminaBonus;
        }
        // 3. Loop through learned skills and add their bonuses.
        foreach (Skill skill in learnedSkills)
        {
            // We only care about passive skills for this calculation.
            if (skill.skillType == SkillType.Passive)
            {
                switch (skill.passiveEffectType)
                {
                    case SkillEffectType.Passive_IncreaseStrength:
                        strength_bonus += skill.passiveEffectMagnitude;
                        break;
                    case SkillEffectType.Passive_IncreaseAgility:
                        agility_bonus += skill.passiveEffectMagnitude;
                        break;
                    case SkillEffectType.Passive_IncreaseStamina:
                        stamina_bonus += skill.passiveEffectMagnitude;
                        break;
                    case SkillEffectType.Passive_IncreaseIntellect:
                        intellect_bonus += skill.passiveEffectMagnitude;
                        break;
                }
            }
        }
        // 4. Calculate the final primary attributes.
        // NOTE: these are LOCAL only. We must never write them back into
        // playerStats.strength/etc., or the bonuses would be baked into the
        // base values and compound on every recalculation.
        int finalStamina = playerStats.stamina + stamina_bonus;
        int finalStrength = playerStats.strength + strength_bonus;
        int finalAgility = playerStats.agility + agility_bonus;
        int finalIntellect = playerStats.intellect + intellect_bonus;
        // 5. Calculate all secondary stats based on the final primary attributes.
        //    Note: temporary IncreaseAttack/DecreaseAttack and defense status
        //    effects are NOT baked in here. They are applied fresh at combat
        //    time (see GetAttackBonusFromEffects / PerformStandardAttack) because
        //    effects are added and expire without triggering a recalculation.
        playerStats.maxMana = engineSettings.baseMana + (finalIntellect * engineSettings.manaPerIntellect);
        playerStats.maxHealth = engineSettings.baseHealth + (finalStamina * engineSettings.healthPerStamina);
        playerStats.baseAttack = engineSettings.baseAttackValue + (finalStrength * engineSettings.attackPerStrength) + equipmentAttack;
        playerStats.dodgeChance = Mathf.Clamp(
            engineSettings.baseDodgeChance + (finalAgility * engineSettings.dodgePerAgility), 0f, engineSettings.maxDodgeChance);
        playerStats.hitChance = Mathf.Clamp(
            engineSettings.baseHitChance + (finalAgility * engineSettings.hitPerAgility), 0f, engineSettings.maxHitChance);
        // 6. Clamp current health and mana so they don't exceed the new maximums.
        playerCurrentHealth = Mathf.Min(playerCurrentHealth, playerStats.maxHealth);
        playerStats.currentMana = Mathf.Min(playerStats.currentMana, playerStats.maxMana);
    }

    void HandleMinigameMove(string inputText)
    {
        if (!isPlayerTurn)
        {
            LogText($"Wait for {minigameOpponentName} to make a move!", TextType.GameResponse);
            return;
        }
        string formattedInput = inputText.ToLower();
        if (formattedInput == "quit" || formattedInput == "q")
        {
            EndConnectFour("You forfeit the game and step away from the board.");
            return;
        }
        // 1. Validate Input: Try to convert the input to a column number.
        if (!int.TryParse(inputText, out int columnNumber) || columnNumber < 1 || columnNumber > C4_COLS)
        {
            LogText("Invalid move. Please type a column number from 1 to 7 or 'quit'");
            return;
        }
        int chosenColumn = columnNumber - 1;
        // 2. Find Available Row: Check for the lowest empty spot in that column.
        int chosenRow = -1; // -1 means the column is full.
        for (int row = C4_ROWS - 1; row >= 0; row--)
        {
            if (connectFourBoard[row, chosenColumn] == 0) // 0 means empty
            {
                chosenRow = row;
                break;
            }
        }
        // 3. Validate Move: Check if the column was full.
        if (chosenRow == -1)
        {
            LogText("That column is full! Try another.");
            return;
        }
        // 4. Place the Piece: Update the board state with the player's move.
        connectFourBoard[chosenRow, chosenColumn] = 1; // 1 for player
        // 5. Display the updated board.
        DisplayConnectFourBoard();
        if (CheckForWin(chosenRow, chosenColumn, 1))
        {
            ProcessMinigameEnd(playerWon: true);
            return;
        }

        if (CheckForTie())
        {
            ProcessMinigameEnd(playerWon: false, isTie: true);
            return;
        }
        TakeAITurn();
    }

    public void DisplayLocation(bool useTypewriter = true)
    {
        // 1. Get the complete room description string.
        string locationDescription = BuildLocationDescriptionString();
        // 2. Queue it up to be printed, clearing the screen.
        PrintToScreen(locationDescription, useTypewriter);
    }

    void AttemptToMove(string direction)
    {
        // Expand single-letter shortcuts ("go n" -> "go north").
        if (directionAliases.TryGetValue(direction, out string fullDirection)) direction = fullDirection;
        foreach (Exit exit in currentLocation.exits)
        {
            if (exit.direction.ToLower() == direction)
            {
                if (exitVisibilityState[exit] == false) { continue; }
                if (exit.exitAction == ExitActionType.BlockIfHoldingItem && exit.blockingItem != null &&
                    playerInventory.Any(inst => inst.blueprint == exit.blockingItem)) { LogText(exit.blockedMessage); return; }
                if (exitLockedState[exit] == true) { LogText(exit.lockedDescription); return; }
                Location destination = exit.destination;
                string actionMessage = ProcessExitAction(exit);
                if (currentGameState == GameState.GameOver)
                {
                    if (!string.IsNullOrEmpty(actionMessage)) { LogText(actionMessage, TextType.Narrative); }
                    return;
                }
                previousLocation = currentLocation;
                currentLocation = destination;
                onLocationChanged.Invoke(destination);
                if (!string.IsNullOrEmpty(actionMessage))
                {
                    PrintToScreen(actionMessage, true);
                    string locationDescription = BuildLocationDescriptionString();
                    LogText(locationDescription, TextType.Narrative, false);
                }
                else
                {
                    DisplayLocation();
                }
                // Ambushers strike no matter which kind of exit you arrived by.
                CheckForAmbushes();
                return;
            }
        }
        LogText("You can't go " + direction + ".");
    }

    private string ProcessExitAction(Exit exit)
    {
        if (exit.exitAction == ExitActionType.None)
        {
            return null;
        }
        switch (exit.exitAction)
        {
            case ExitActionType.StripAllItemsAndEquipment:
                playerInventory.Clear();
                equippedWeapon = null;
                equippedArmor = null;
                return "As you pass through the doorway, a magical barrier hums and you feel the weight of your gear vanish from your person. You are left with nothing.";
            case ExitActionType.InstaDeath:
                // Set the player's health to 0 and end the game.
                playerCurrentHealth = 0;
                currentGameState = GameState.GameOver;
                // Return the specific death message from the Exit asset.
                return exit.deathMessage;
            case ExitActionType.ChangeLocationState:
                // Check if the required fields are set to prevent errors.
                if (exit.targetLocation != null && exit.itemsToReveal != null && exit.itemsToReveal.Count > 0)
                {
                    // Loop through every item in the "itemsToReveal" list.
                    foreach (Item item in exit.itemsToReveal)
                    {
                        // Add each item to the runtime list for the target location.
                        // We still check to make sure we don't add duplicates.
                        if (!roomItemsState[exit.targetLocation].Any(inst => inst.blueprint == item))
                        {
                            roomItemsState[exit.targetLocation].Add(new ItemInstance(item));
                        }
                    }
                }
                // Return the message to be displayed.
                return exit.stateChangeMessage;
        }
        return null;
    }

    public void LoadScenario(TextEngineScenario scenario)
    {
        // 0) Announce what we’re about to do
        LogText($"<i>Loading scenario:</i> {scenario.scenarioName}");
        // 1) Look up the location
        var loc = FindLocationByName(scenario.startingLocation);
        if (loc == null)
        {
            LogText($"<color=red>ERROR:</color> Could not find location “{scenario.startingLocation}”.");
            return;
        }
        currentLocation = loc;
        // 2) Reset inventory
        playerInventory.Clear();
        foreach (var item in scenario.startingInventory)
            if (item != null) playerInventory.Add(new ItemInstance(item));
        // 3) Reset enemies
        if (!roomEnemiesState.ContainsKey(currentLocation))
            roomEnemiesState[currentLocation] = new List<EnemyInstance>();
        var enemiesHere = roomEnemiesState[currentLocation];
        enemiesHere.Clear();
        foreach (var e in scenario.enemiesInLocation)
            enemiesHere.Add(new EnemyInstance(e));
        // 4) Reset player health
        if (scenario.resetPlayerHealth)
            playerCurrentHealth = playerStats.maxHealth;
        // 5) ALWAYS rebuild the world/UI for this new location…
        //    …but skip the slow typewriter for the description.
        //    (Assumes you’ve added an overload or parameter to disable it.)
        DisplayLocation(useTypewriter: false);
        // 6) And still log into your console so you see it there too
        LogText($"<b>You are now at:</b> {currentLocation.name}");
        LogText(currentLocation.description);
    }

    void LogText(string textToLog, TextType type = TextType.GameResponse, bool useTypewriter = true)
    {
        textRenderer.Log(textToLog, type, useTypewriter);
    }

    private string HighlightKeywords(string text)
    {
        // If we have no location or no state dictionaries, bail out
        if (currentLocation == null
         || roomItemsState == null
         || interactableStates == null
         || roomCharactersState == null
         || roomEnemiesState == null)
            return text;
        // Highlight items
        if (roomItemsState.TryGetValue(currentLocation, out var itemsInRoom) && itemsInRoom != null)
        {
            foreach (var instance in itemsInRoom)
            {
                var item = instance?.blueprint;
                if (item == null) continue;
                string pattern = $@"\b{Regex.Escape(item.itemName)}\b";
                text = Regex.Replace(text, pattern,
                    $"<color={keywordColor}>{item.itemName}</color>",
                    RegexOptions.IgnoreCase);
            }
        }
        // Highlight interactables
        var inters = currentLocation.interactables ?? new List<Interactable>();
        foreach (var interactable in inters)
        {
            if (interactable == null) continue;
            string pattern = $@"\b{Regex.Escape(interactable.noun)}\b";
            text = Regex.Replace(text, pattern,
                $"<color={keywordColor}>{interactable.noun}</color>",
                RegexOptions.IgnoreCase);
        }
        // Highlight characters
        if (roomCharactersState.TryGetValue(currentLocation, out var chars) && chars != null)
        {
            foreach (var character in chars)
            {
                if (character == null) continue;
                string pattern = $@"\b{Regex.Escape(character.characterName)}\b";
                text = Regex.Replace(text, pattern,
                    $"<color={keywordColor}>{character.characterName}</color>",
                    RegexOptions.IgnoreCase);
            }
        }
        // Highlight enemies
        if (roomEnemiesState.TryGetValue(currentLocation, out var enemies) && enemies != null)
        {
            foreach (var enemyInstance in enemies)
            {
                if (enemyInstance?.enemyBlueprint == null) continue;
                string name = enemyInstance.enemyBlueprint.enemyName;
                string pattern = $@"\b{Regex.Escape(name)}\b";
                text = Regex.Replace(text, pattern,
                    $"<color={enemyColor}>{name}</color>",
                    RegexOptions.IgnoreCase);
            }
        }
        return text;
    }

    // World lookups live in WorldState; these forward for readability at call sites.
    private Location FindLocationByName(string name) => world.FindLocationByName(name);
    private Item FindItemByName(string name) => world.FindItemByName(name);
    private Exit FindExit(Location loc, string direction) => world.FindExit(loc, direction);
    private Location FindLocationOfExit(Exit exitToFind) => world.FindLocationOfExit(exitToFind);

    private string BuildLocationDescriptionString()
    {
        // Nothing to describe yet (e.g. during a load, before HandleLoad has
        // assigned currentLocation). Bail early so we never index a dictionary
        // with a null key, which throws ArgumentNullException.
        if (currentLocation == null) return "";
        var sb = new StringBuilder();
        // Base description
        sb.Append(currentLocation?.description ?? "");
        // Items in room (guard against missing key or null list)
        List<ItemInstance> items = roomItemsState.ContainsKey(currentLocation)
            ? roomItemsState[currentLocation] ?? new List<ItemInstance>()
            : new List<ItemInstance>();
        // De-duplicate identical stackable items so a repeated blueprint doesn't
        // print its descriptionInRoom sentence multiple times.
        var describedItems = new HashSet<Item>();
        foreach (var instance in items)
        {
            var item = instance?.blueprint;
            if (item == null) continue;
            if (item.isStackable && !describedItems.Add(item)) continue;
            sb.Append(" " + (item.descriptionInRoom ?? ""));
        }
        // Interactables
        List<Interactable> interactables = roomInteractablesState.ContainsKey(currentLocation)
            ? roomInteractablesState[currentLocation] ?? new List<Interactable>()
            : new List<Interactable>();
        foreach (var intr in interactables)
            sb.Append(" " + (intr?.description ?? ""));
        // Characters
        List<Character> chars = roomCharactersState.ContainsKey(currentLocation)
            ? roomCharactersState[currentLocation] ?? new List<Character>()
            : new List<Character>();
        foreach (var c in chars)
            sb.Append(" " + (c?.descriptionInRoom ?? ""));
        // Enemies
        List<EnemyInstance> enemies = roomEnemiesState.ContainsKey(currentLocation)
            ? roomEnemiesState[currentLocation] ?? new List<EnemyInstance>()
            : new List<EnemyInstance>();
        foreach (var e in enemies)
            sb.Append(" " + (e?.enemyBlueprint?.description ?? ""));
        // Exits
        sb.Append("\n\nObvious exits are to the:");
        string cameFromDirection = null;
        if (previousLocation != null)
        {
            foreach (var exit in currentLocation.exits ?? new Exit[0])
                if (exit.destination == previousLocation)
                {
                    cameFromDirection = exit.direction;
                    break;
                }
        }
        foreach (var exit in currentLocation.exits ?? new Exit[0])
        {
            bool visible = exitVisibilityState.ContainsKey(exit) && exitVisibilityState[exit];
            if (!visible) continue;
            string line = $"\n- {exit.direction}";
            if (exit.direction == cameFromDirection) line += "*";
            sb.Append(line);
        }
        // Finally highlight keywords
        return HighlightKeywords(sb.ToString());
    }

    private Character FindCharacterByName(string name) => world.FindCharacterByName(name);
    private Enemy FindEnemyBlueprintByName(string name) => world.FindEnemyBlueprintByName(name);

    #region Progression
    private void GrantXp(int amount)
    {
        if (amount <= 0) return;
        // With leveling disabled, XP is meaningless — don't award or announce it.
        if (!engineSettings.useLevelingSystem) return;

        playerStats.currentXp += amount;
        LogText($"You gain {amount} XP.", TextType.GameResponse);

        // Check if the player has enough XP to level up.
        // A while loop handles the case of gaining multiple levels at once.
        while (playerStats.currentXp >= playerStats.xpToNextLevel)
        {
            playerStats.currentXp -= playerStats.xpToNextLevel;
            playerStats.level++;
            playerStats.skillPoints += engineSettings.skillPointsPerLevel;
            // Give a base attribute point on level up for the player to spend later, or automatically grant stats
            playerStats.stamina += engineSettings.staminaPerLevel;
            playerStats.strength += engineSettings.strengthPerLevel;
            playerStats.xpToNextLevel = Mathf.RoundToInt(playerStats.xpToNextLevel * engineSettings.xpCurveMultiplier);
            RecalculatePlayerStats(); // Recalculate all stats with the new base values
            playerCurrentHealth = playerStats.maxHealth; // Full heal
            LogText($"<color=yellow>LEVEL UP! You are now level {playerStats.level}!</color>", TextType.Narrative);
            LogText($"Your stats have increased! You gained a skill point.", TextType.GameResponse);
        }
    }
    #endregion
}