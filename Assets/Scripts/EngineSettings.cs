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
}
