namespace TextEngine
{
    using UnityEngine;

    public enum EffectType
    {
        DamageOverTime,
        HealOverTime,
        IncreaseAttack,
        DecreaseAttack,
        IncreaseDefense,
        DecreaseDefense
    }

    [CreateAssetMenu(menuName = "Text Adventure/Status Effect")]
    public class StatusEffect : ScriptableObject
    {
        public string effectName;
        public EffectType effectType;
        public int magnitude; // How much damage/healing/stat change
        [Tooltip("How long this effect lasts, in seconds")]
        public float durationInSeconds = 3f;
        [Tooltip("Seconds between each tick")]
        public float tickInterval = 1f;
        [TextArea]
        public string applicationMessage; // e.g., "You are poisoned!"
        [TextArea]
        public string effectMessage; // e.g., "The poison courses through your veins."
        [Range(0, 1)] // This makes it a nice 0-100% slider in the Inspector
        [Tooltip("The chance of this effect being applied (1 = 100%, 0.5 = 50%).")]
        public float chanceToApply = 1f; // Default to 100% so existing effects don't break
    }
}
