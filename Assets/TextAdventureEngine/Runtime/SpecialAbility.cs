namespace TextEngine
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "Text Adventure/Special Ability", order = 14)]
    public class SpecialAbility : ScriptableObject
    {
        [Range(0, 1)] // Restricts the value to a 0-1 range in the Inspector
        public float chanceToUse = 0.3f; // The probability of this ability being used

        [TextArea(3, 5)]
        public string successDescription; // The text shown when the ability is used

        public AbilityEffect effect; // The type of effect this ability has

        public StatusEffect effectToApplyOnSuccess; // e.g., a venomous bite applies a "Poison" effect

        public int magnitude;
    }
}
