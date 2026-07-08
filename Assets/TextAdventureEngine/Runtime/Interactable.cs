namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic;

    [System.Serializable]
    public class Interaction
    {
        [Tooltip("The verb that triggers this interaction (e.g., 'flush', 'pull'). Must match an Action keyword. Leave blank for item-based interactions ('use item on target').")]
        public string interactionVerb;

        [Tooltip("The state this interaction is valid in. If the interactable's current state matches this, this interaction can trigger.")]
        public string requiredState;

        [Tooltip("An optional item the player must use to trigger this interaction.")]
        public Item requiredItem;

        [Tooltip("A list of effects that will happen in order when this interaction is successfully triggered.")]
        public List<InteractionEffect> effects = new List<InteractionEffect>();
    }

    [CreateAssetMenu(menuName = "Text Adventure/Interactable")]
    public class Interactable : ScriptableObject
    {
        public string noun;

        [TextArea(3, 5)]
        public string description;

        [TextArea(3, 5)]
        public string detailedDescription;

        [Tooltip("The state this object starts in when the game begins.")]
        public string initialState = "default";

        [Header("Custom Actions")]
        [Tooltip("A list of custom actions that can be performed on this interactable.")]
        public List<CustomAction> allowedCustomActions = new List<CustomAction>();

        [Tooltip("The list of all possible interactions for this object.")]
        public Interaction[] interactions;
    }
}
