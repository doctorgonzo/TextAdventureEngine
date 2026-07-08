namespace TextEngine.EditorTools
{
    using System.Collections.Generic;
    using System.Linq;
    using TextEngine;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Tools ▸ Text Engine ▸ Validate Content — scans every content asset for
    /// broken references and authoring mistakes and reports them with
    /// severity and a click-to-ping button. Turns the engine's implicit
    /// authoring rules into enforced ones.
    /// </summary>
    public class ContentValidatorWindow : EditorWindow
    {
        private enum Severity { Error, Warning, Info }

        private class Finding
        {
            public Severity severity;
            public string message;
            public Object asset;
        }

        private readonly List<Finding> findings = new List<Finding>();
        private Vector2 scrollPos;
        private bool hasRun;

        private static readonly HashSet<string> CompassDirectionsSet =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "North", "East", "South", "West", "Up", "Down" };

        [MenuItem("Tools/Text Engine/Validate Content")]
        public static void ShowWindow() => GetWindow<ContentValidatorWindow>("Content Validator");

        private void OnGUI()
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("Validate All Content", GUILayout.Height(30)))
            {
                RunValidation();
            }
            EditorGUILayout.Space();

            if (!hasRun)
            {
                EditorGUILayout.HelpBox("Scans every Location, Item, Enemy, Character, Dialogue Node, and Quest for broken references, misconfigurations, and save-breaking name collisions.", MessageType.Info);
                return;
            }

            int errors = findings.Count(f => f.severity == Severity.Error);
            int warnings = findings.Count(f => f.severity == Severity.Warning);
            if (findings.Count == 0)
            {
                EditorGUILayout.HelpBox("All content checks passed. ✔", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField($"{errors} error(s), {warnings} warning(s), {findings.Count - errors - warnings} info", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var finding in findings)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                var messageType = finding.severity == Severity.Error ? MessageType.Error
                    : finding.severity == Severity.Warning ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox(finding.message, messageType);
                if (finding.asset != null && GUILayout.Button("Ping", GUILayout.Width(45), GUILayout.Height(38)))
                {
                    EditorGUIUtility.PingObject(finding.asset);
                    Selection.activeObject = finding.asset;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Add(Severity severity, Object asset, string message) =>
            findings.Add(new Finding { severity = severity, asset = asset, message = message });

        private static List<T> LoadAll<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
                .Where(a => a != null)
                .ToList();

        private void RunValidation()
        {
            findings.Clear();
            hasRun = true;

            var locations = LoadAll<Location>();
            var dialogueNodes = LoadAll<DialogueNode>();
            var items = LoadAll<Item>();
            var quests = LoadAll<Quest>();
            var enemies = LoadAll<Enemy>();
            var characters = LoadAll<Character>();
            var registry = LoadAll<FlagRegistry>().FirstOrDefault();

            ValidateLocations(locations);
            ValidateDialogue(dialogueNodes, registry);
            ValidateItems(items);
            ValidateQuests(quests);
            ValidateCharacters(characters);
            ValidateEnemies(enemies);
            ValidateResourcesPlacement();
            ValidateUniqueNames(locations, items, quests, enemies);

            // Errors first, then warnings, then info.
            findings.Sort((a, b) => a.severity.CompareTo(b.severity));
        }

        private void ValidateLocations(List<Location> locations)
        {
            foreach (var loc in locations)
            {
                if (loc.exits != null)
                {
                    foreach (var exit in loc.exits)
                    {
                        if (exit == null) continue;
                        string label = $"'{loc.name}' exit '{exit.direction}'";
                        if (exit.destination == null)
                            Add(Severity.Error, loc, $"{label}: no destination assigned — 'go {exit.direction?.ToLower()}' will throw.");
                        if (string.IsNullOrEmpty(exit.direction))
                            Add(Severity.Error, loc, $"'{loc.name}': an exit has an empty direction and can never be used.");
                        else if (!CompassDirectionsSet.Contains(exit.direction))
                            Add(Severity.Warning, loc, $"{label}: direction is not one of the six compass directions (N/E/S/W/Up/Down), so the World Map editor and 'go' shortcuts won't recognize it.");
                        if (exit.isLocked && exit.keyToUnlock == null)
                            Add(Severity.Error, loc, $"{label}: locked but no key assigned — it can never be unlocked.");
                        if (exit.isLocked && string.IsNullOrEmpty(exit.lockedDescription))
                            Add(Severity.Warning, loc, $"{label}: locked but has no locked description; the player sees nothing when blocked.");
                        if (exit.exitAction == ExitActionType.InstaDeath && string.IsNullOrEmpty(exit.deathMessage))
                            Add(Severity.Warning, loc, $"{label}: InstaDeath without a death message.");
                        if (exit.exitAction == ExitActionType.BlockIfHoldingItem && exit.blockingItem == null)
                            Add(Severity.Error, loc, $"{label}: BlockIfHoldingItem without a blocking item — it never blocks.");
                        if (exit.exitAction == ExitActionType.ChangeLocationState &&
                            (exit.targetLocation == null || exit.itemsToReveal == null || exit.itemsToReveal.Count == 0))
                            Add(Severity.Warning, loc, $"{label}: ChangeLocationState without a target location and items to reveal does nothing.");
                    }
                }
                CheckForNullEntries(loc, loc.itemsInLocation, "Items In Location");
                CheckForNullEntries(loc, loc.interactables, "Interactables");
                CheckForNullEntries(loc, loc.charactersInLocation, "Characters In Location");
                CheckForNullEntries(loc, loc.defaultEnemies, "Default Enemies");
                if (loc.isShop && (loc.shopInventory == null || loc.shopInventory.Count == 0))
                    Add(Severity.Warning, loc, $"'{loc.name}' is a shop with an empty inventory.");
                if (!loc.isShop && loc.shopInventory != null && loc.shopInventory.Count > 0)
                    Add(Severity.Warning, loc, $"'{loc.name}' has a shop inventory but 'Is Shop' is off — the shop can never open.");
            }
        }

        private void CheckForNullEntries<T>(Object owner, List<T> list, string listName) where T : class
        {
            if (list == null) return;
            int nulls = list.Count(entry => entry == null);
            if (nulls > 0)
                Add(Severity.Warning, owner, $"'{owner.name}': {listName} contains {nulls} empty entr{(nulls == 1 ? "y" : "ies")}.");
        }

        private void ValidateDialogue(List<DialogueNode> nodes, FlagRegistry registry)
        {
            foreach (var node in nodes)
            {
                if (registry != null)
                {
                    foreach (var flag in node.requiredFlags.Where(f => !string.IsNullOrEmpty(f) && !registry.flags.Contains(f)))
                        Add(Severity.Warning, node, $"Dialogue '{node.name}': required flag '{flag}' is not in the Flag Registry.");
                    foreach (var flag in node.flagsToSet.Where(f => !string.IsNullOrEmpty(f) && !registry.flags.Contains(f)))
                        Add(Severity.Warning, node, $"Dialogue '{node.name}': flag to set '{flag}' is not in the Flag Registry.");
                }
                if (node.requiredFlags.Count > 0 && node.failureNode == null)
                    Add(Severity.Info, node, $"Dialogue '{node.name}': has required flags but no failure node — the conversation silently ends when the check fails.");

                if (node.playerResponses != null)
                {
                    for (int i = 0; i < node.playerResponses.Length; i++)
                    {
                        if (string.IsNullOrEmpty(node.playerResponses[i]?.responseText))
                            Add(Severity.Warning, node, $"Dialogue '{node.name}': response {i + 1} has empty text.");
                    }
                }

                if (node.successActions == null) continue;
                foreach (var action in node.successActions)
                {
                    if (action == null) continue;
                    switch (action.actionType)
                    {
                        case DialogueActionType.GiveItem:
                        case DialogueActionType.TakeItem:
                            if (action.item == null)
                                Add(Severity.Error, node, $"Dialogue '{node.name}': {action.actionType} with no item assigned.");
                            break;
                        case DialogueActionType.RevealExit:
                            if (string.IsNullOrEmpty(action.exitDirection))
                                Add(Severity.Error, node, $"Dialogue '{node.name}': RevealExit with no direction.");
                            break;
                        case DialogueActionType.StartQuest:
                        case DialogueActionType.CompleteQuest:
                            if (action.quest == null)
                                Add(Severity.Error, node, $"Dialogue '{node.name}': {action.actionType} with no quest assigned.");
                            break;
                        case DialogueActionType.UpdateQuest:
                            if (action.quest == null)
                                Add(Severity.Error, node, $"Dialogue '{node.name}': UpdateQuest with no quest assigned.");
                            else if (action.objectiveIndex < 0 || action.objectiveIndex >= action.quest.objectives.Count)
                                Add(Severity.Error, node, $"Dialogue '{node.name}': UpdateQuest objective index {action.objectiveIndex} is out of range for '{action.quest.questName}' ({action.quest.objectives.Count} objectives).");
                            break;
                    }
                }
            }
        }

        private void ValidateItems(List<Item> items)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.itemName))
                    Add(Severity.Error, item, $"Item asset '{item.name}' has an empty Item Name — the player can never refer to it.");
                if (item.sellPrice > item.buyPrice && item.buyPrice > 0)
                    Add(Severity.Warning, item, $"Item '{item.itemName}': sell price ({item.sellPrice}) exceeds buy price ({item.buyPrice}) — players can buy and re-sell for infinite money.");
                if (item.itemType == ItemType.Currency && item.sellPrice <= 0)
                    Add(Severity.Warning, item, $"Currency item '{item.itemName}' has no sell price — picking it up grants 0 coins.");
            }
        }

        private void ValidateQuests(List<Quest> quests)
        {
            foreach (var quest in quests)
            {
                if (quest.objectives == null || quest.objectives.Count == 0)
                    Add(Severity.Warning, quest, $"Quest '{quest.questName}' has no objectives.");
                if (quest.itemRewards != null && quest.itemRewards.Any(r => r == null))
                    Add(Severity.Warning, quest, $"Quest '{quest.questName}' has empty entries in its item rewards.");
            }
        }

        private void ValidateCharacters(List<Character> characters)
        {
            foreach (var character in characters)
            {
                if (string.IsNullOrEmpty(character.characterName))
                    Add(Severity.Error, character, $"Character asset '{character.name}' has an empty Character Name.");
                if (character.startingDialogue == null)
                    Add(Severity.Info, character, $"Character '{character.characterName}' has no starting dialogue — 'talk to' reports they have nothing to say.");
            }
        }

        private void ValidateEnemies(List<Enemy> enemies)
        {
            foreach (var enemy in enemies)
            {
                if (string.IsNullOrEmpty(enemy.enemyName))
                    Add(Severity.Error, enemy, $"Enemy asset '{enemy.name}' has an empty Enemy Name.");
                if (enemy.maxHealth <= 0)
                    Add(Severity.Error, enemy, $"Enemy '{enemy.enemyName}' has {enemy.maxHealth} max health — it dies instantly.");
                if (enemy.lootDrops != null && enemy.lootDrops.Any(l => l == null))
                    Add(Severity.Warning, enemy, $"Enemy '{enemy.enemyName}' has empty entries in its loot drops.");
                if (!string.IsNullOrEmpty(enemy.exitDirectionToReveal) && !CompassDirectionsSet.Contains(enemy.exitDirectionToReveal))
                    Add(Severity.Warning, enemy, $"Enemy '{enemy.enemyName}': exit direction to reveal '{enemy.exitDirectionToReveal}' is not a compass direction.");
            }
        }

        // The runtime catalogs only see assets inside Resources folders. Any
        // content asset outside one silently vanishes from loaded saves.
        private void ValidateResourcesPlacement()
        {
            CheckPlacement<Location>();
            CheckPlacement<Item>();
            CheckPlacement<Enemy>();
            CheckPlacement<Quest>();
            CheckPlacement<StatusEffect>();
            CheckPlacement<Skill>();
            CheckPlacement<Action>();
        }

        private void CheckPlacement<T>() where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Resources/"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    Add(Severity.Error, asset, $"{typeof(T).Name} '{asset.name}' is outside a Resources folder ({path}) — the engine's catalogs cannot load it, and saves referencing it will silently drop it.");
                }
            }
        }

        // Saves reference content by asset name; duplicates collide on load.
        private void ValidateUniqueNames(List<Location> locations, List<Item> items, List<Quest> quests, List<Enemy> enemies)
        {
            CheckUnique(locations, "Location");
            CheckUnique(items, "Item");
            CheckUnique(quests, "Quest");
            CheckUnique(enemies, "Enemy");
        }

        private void CheckUnique<T>(List<T> assets, string kind) where T : Object
        {
            foreach (var group in assets.GroupBy(a => a.name).Where(g => g.Count() > 1))
            {
                Add(Severity.Error, group.First(), $"{group.Count()} {kind} assets share the name '{group.Key}' — saves are name-keyed, so their state collides on load.");
            }
        }
    }
}
