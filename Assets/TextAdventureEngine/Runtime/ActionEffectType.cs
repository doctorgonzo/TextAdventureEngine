namespace TextEngine
{
    using UnityEngine;

    public enum ActionEffectType
    {
        // --- Messaging & State ---
        LogMessage,                 // Displays text to the player.
        SetFlag,                    // Sets a world flag to true.
        ChangeTargetState,          // Changes the state of the interactable that was targeted.
        // --- Player & Inventory ---
        GiveItemToPlayer,           // Gives the player a specific item.
        TakeItemFromPlayer,         // Removes a specific item from the player's inventory.
        HealPlayer,                 // Heals the player by a specific amount.
        DamagePlayer,               // Damages the player by a specific amount.
        // --- World & Location ---
        MoveToLocation,             // Instantly moves the player to another location.
        DestroyTargetInteractable,  // Removes the interactable from the room permanently.
        // --- Sensory ---
        PlaySound                   // Plays a one-off audio clip.
    }


    [System.Serializable]
    public class ActionEffect
    {
        public ActionEffectType effectType;

        [Header("Parameters")]
        [Tooltip("For LogMessage, SetFlag, or ChangeTargetState.")]
        public string stringParameter;

        [Tooltip("For GiveItemToPlayer or TakeItemFromPlayer.")]
        public Item itemParameter;

        [Tooltip("For HealPlayer or DamagePlayer.")]
        public int intParameter;

        [Tooltip("For MoveToLocation.")]
        public Location locationParameter;

        [Tooltip("For PlaySound.")]
        public AudioClip audioClipParameter;
    }
}
