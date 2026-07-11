namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic;

    // A data-driven attack verb. Unlike the built-in "attack" (hardcoded in
    // GameController.Combat.cs), the damage, accuracy, and targeting rules here
    // all come from the asset, so designers can author backstabs, power attacks,
    // provoking strikes, etc. without writing any code. Drop instances into a
    // Resources/Actions/Combat folder and the parser picks them up automatically.
    [CreateAssetMenu(menuName = "Text Adventure/Combat Action", order = 2)]
    public class CombatAction : ScriptableObject
    {
        [Tooltip("The verb the player types (e.g., 'backstab', 'smash').")]
        public string keyword;

        [Tooltip("Other words that trigger this same attack.")]
        public List<string> synonyms = new List<string>();

        [Header("Damage")]
        [Tooltip("Multiplies the player's base attack. 1 = a normal hit, 2.5 = a heavy hit.")]
        public float damageMultiplier = 1f;

        [Tooltip("Flat damage added after the multiplier.")]
        public int bonusDamage = 0;

        [Header("Accuracy")]
        [Tooltip("Added to the player's hit chance for this attack. Negative makes it riskier (e.g. -0.15 for a wild swing).")]
        public float hitChanceModifier = 0f;

        [Header("Targeting")]
        [Tooltip("If true, this verb can be used on a friendly NPC, turning them hostile (via the Character's 'Becomes Enemy') before striking.")]
        public bool provokesNPCs = false;

        [Tooltip("If true, this attack only lands against a target that isn't yet aware of the player — e.g. a freshly-provoked NPC. Alert enemies refuse it (shows the failure message).")]
        public bool onlyIfEnemyUnaware = false;

        [Header("Turn Flow")]
        [Tooltip("If true, the target takes its turn after this attack, like a normal attack. Uncheck for pure hit-and-run openers.")]
        public bool enemyRetaliates = true;

        [Header("Messaging")]
        [TextArea(2, 4)]
        [Tooltip("Optional line shown just before the hit resolves (e.g. 'You slip into the shadows...').")]
        public string flavorMessage;

        [TextArea(2, 4)]
        [Tooltip("Shown when the attack can't be used here — no valid target, or an alert enemy for an unaware-only strike.")]
        public string failureMessage = "You can't do that right now.";
    }
}
