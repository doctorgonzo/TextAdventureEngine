namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic;

    [CreateAssetMenu(menuName = "Text Adventure/Custom Action", order = 2)]
    public class CustomAction : ScriptableObject
    {
        [Tooltip("The main verb for this action (e.g., 'read', 'climb').")]
        public string keyword;

        [Tooltip("Other words the player might type for this action.")]
        public List<string> synonyms = new List<string>();

        [TextArea(3, 5)]
        [Tooltip("The message to display if the player tries to use this verb on the wrong thing, or on nothing.")]
        public string failureMessage = "You can't seem to do that.";

        [Tooltip("The list of effects that will happen in order when this action is successfully used on a valid target.")]
        public List<ActionEffect> effects = new List<ActionEffect>();
    }
}
