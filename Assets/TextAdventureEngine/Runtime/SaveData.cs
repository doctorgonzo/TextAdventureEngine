namespace TextEngine
{
    using System.Collections.Generic;
    [System.Serializable]
    public class RoomInteractables
    {
        public string locationName;
        // Interactable asset names (not nouns), so two rooms can each have e.g. a
        // "lever" without their saved state colliding.
        public List<string> interactableNames = new List<string>();
    }
    [System.Serializable]
    public class SaveData
    {
        // --- Player State ---
        public PlayerStats playerStats; // Level, xp, currency, attributes, etc.
        public int playerCurrentHealth; // Still tracked separately.
        public string playerName;
        public float playerDrunkenness;
        public string currentLocationName;
        // --- Quests ---
        public List<ActiveQuestSaveState> activeQuests = new List<ActiveQuestSaveState>();
        public List<string> completedQuestNames = new List<string>();
        // --- Active Status Effects ---
        public List<StatusEffectSaveState> activeStatusEffects = new List<StatusEffectSaveState>();
        // --- Learned Skills ---
        public List<string> learnedSkillNames = new List<string>();
        // --- Equipment State ---
        public string equippedWeaponName;
        public string equippedArmorName;
        // --- Inventory State ---
        public List<string> playerInventoryItemNames = new List<string>();
        // --- World State (using helper classes for serialization) ---
        public List<RoomInteractables> roomInteractablesState = new List<RoomInteractables>();
        public List<RoomItems> roomItemsState = new List<RoomItems>();
        public List<ExitState> exitLockedState = new List<ExitState>();
        public List<ExitState> exitVisibilityState = new List<ExitState>();
        public List<InteractableState> interactableStates = new List<InteractableState>();
        public List<EnemySaveState> roomEnemiesState = new List<EnemySaveState>();
        public List<RoomCharacters> roomCharactersState = new List<RoomCharacters>();
        public List<ShopSaveState> shopStates = new List<ShopSaveState>();
        public List<string> trueFlags = new List<string>();
    }
    [System.Serializable]
    public class RoomCharacters
    {
        public string locationName;
        public List<string> characterNames = new List<string>();
    }
    //--- Helper classes for serializing dictionaries ---

    [System.Serializable]
    public class RoomItems
    {
        public string locationName;
        public List<string> itemNames = new List<string>();
    }

    [System.Serializable]
    public class ExitState
    {
        public string locationName;
        public string exitDirection;
        public bool state;
    }

    [System.Serializable]
    public class InteractableState
    {
        // Identified by location + asset name so identically-named interactables
        // in different rooms stay independent.
        public string locationName;
        public string interactableName;
        public string state;
    }

    [System.Serializable]
    public class ActiveQuestSaveState
    {
        public string questName; // Quest asset name
        public List<bool> objectivesCompleted = new List<bool>();
    }

    [System.Serializable]
    public class StatusEffectSaveState
    {
        public string effectName; // StatusEffect asset name
        public float remainingTime;
    }

    [System.Serializable]
    public class EnemySaveState
    {
        public string locationName;
        public string enemyBlueprintName;
        public int currentHealth;
    }

    [System.Serializable]
    public class ShopSaveState
    {
        public string locationName;
        public List<string> shopItemNames = new List<string>();
    }
}
