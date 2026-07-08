namespace TextEngine
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Text Adventure/Engine Settings", fileName = "NewEngineSettings")]
    public class EngineSettings : ScriptableObject
    {
        [Header("Core Systems")]
        [Tooltip("If true, enables the Level, XP, and Skill Point systems.")]
        public bool useLevelingSystem = true;

        [Tooltip("If true, enables the Strength, Agility, Stamina, and Intellect attributes. If false, the game will use simpler, direct stats.")]
        public bool usePrimaryAttributes = true;

        [Header("Sub-Systems")]
        [Tooltip("If true, enables the drunkenness effect from consuming certain items.")]
        public bool useDrunkennessSystem = true;

        [Tooltip("If true, enables the poison, regen, and other timed status effects.")]
        public bool useStatusEffectSystem = true;

        // You can add any other master switches here in the future.
        // For example:
        // public bool useMagicSystem = false;
        // public bool useItemDurability = false;

        [Header("Balance — Simple Stats (when Primary Attributes are OFF)")]
        [Tooltip("Flat max health used when the primary-attribute system is disabled.")]
        public int simpleMaxHealth = 100;
        [Tooltip("Flat base attack used when the primary-attribute system is disabled.")]
        public int simpleBaseAttack = 10;
        [Range(0f, 1f)]
        [Tooltip("Flat hit chance used when the primary-attribute system is disabled.")]
        public float simpleHitChance = 0.85f;
        [Range(0f, 1f)]
        [Tooltip("Flat dodge chance used when the primary-attribute system is disabled.")]
        public float simpleDodgeChance = 0.10f;

        [Header("Balance — Attribute-Derived Stats (when Primary Attributes are ON)")]
        [Tooltip("Base max health before Stamina is factored in.")]
        public int baseHealth = 50;
        [Tooltip("Max health gained per point of Stamina.")]
        public int healthPerStamina = 10;
        [Tooltip("Base max mana before Intellect is factored in.")]
        public int baseMana = 20;
        [Tooltip("Max mana gained per point of Intellect.")]
        public int manaPerIntellect = 10;
        [Tooltip("Base attack power before Strength is factored in.")]
        public int baseAttackValue = 5;
        [Tooltip("Attack power gained per point of Strength.")]
        public int attackPerStrength = 2;
        [Range(0f, 1f)]
        [Tooltip("Base dodge chance before Agility is factored in.")]
        public float baseDodgeChance = 0.05f;
        [Range(0f, 1f)]
        [Tooltip("Dodge chance gained per point of Agility.")]
        public float dodgePerAgility = 0.01f;
        [Range(0f, 1f)]
        [Tooltip("Maximum dodge chance the player can reach.")]
        public float maxDodgeChance = 0.75f;
        [Range(0f, 1f)]
        [Tooltip("Base hit chance before Agility is factored in.")]
        public float baseHitChance = 0.80f;
        [Range(0f, 1f)]
        [Tooltip("Hit chance gained per point of Agility.")]
        public float hitPerAgility = 0.02f;
        [Range(0f, 1f)]
        [Tooltip("Maximum hit chance the player can reach.")]
        public float maxHitChance = 0.95f;

        [Header("Balance — Leveling")]
        [Tooltip("Each level multiplies the XP required for the next level by this factor.")]
        public float xpCurveMultiplier = 1.5f;
        [Tooltip("Strength granted automatically on each level up.")]
        public int strengthPerLevel = 1;
        [Tooltip("Stamina granted automatically on each level up.")]
        public int staminaPerLevel = 1;
        [Tooltip("Skill points granted on each level up.")]
        public int skillPointsPerLevel = 1;

        [Header("Diagnostics")]
        [Tooltip("If true, the engine logs internal diagnostics (catalog counts on startup, flags as they are set). Leave off for release builds.")]
        public bool verboseLogging = false;

        [Header("Editor Tooling")]
        [Tooltip("Where the editor tools (World Map Graph Editor, inspector 'Create and Add' buttons) place newly created content assets. Must be inside a folder named 'Resources' so the engine's catalogs can load it. Point this at your own game's content folder.")]
        public string contentRootFolder = "Assets/TextAdventureEngine/Demo/Resources";
    }
}
