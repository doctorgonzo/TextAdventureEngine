namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic;

    [CreateAssetMenu(menuName = "Text Adventure/Dialogue Node")]
    public class DialogueNode : ScriptableObject
    {
        [TextArea(5, 10)]
        public string dialogueText;

        public Response[] playerResponses;

        [Header("Node Actions & Conditions")]
        // The item the player MUST have for the success actions to trigger.
        public Item requiredItemForSuccess;
        // The list of actions to perform if the player has the required item.
        public List<DialogueAction> successActions;
        // The node to jump to if the player does NOT have the required item.
        public DialogueNode failureNode;
        [Header("Flag Conditions & Actions")]
        public List<string> requiredFlags = new List<string>(); // Player must have these flags set to TRUE to see this node
        public List<string> flagsToSet = new List<string>();    // These flags will be set to TRUE when this node is displayed
    }
}
