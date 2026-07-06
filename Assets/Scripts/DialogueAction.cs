using UnityEngine;

[System.Serializable]
public class DialogueAction
{
    public DialogueActionType actionType;
    public Item item; // Used for GiveItem and TakeItem
    public string exitDirection; // Used for RevealExit
    [Header("Quest Actions")]
    public Quest quest; // The quest to start, update, or complete
    public int objectiveIndex; // The objective to mark as complete for UpdateQuest
}