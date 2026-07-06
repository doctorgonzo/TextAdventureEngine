using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Text Adventure/Character")]
public class Character : ScriptableObject
{
    public string characterName;

    [TextArea(3, 5)]
    public string descriptionInRoom;

    [TextArea(3, 5)]
    public string detailedDescription;

    public DialogueNode startingDialogue;

    [Header("Hostility")]
    public Enemy becomesEnemy;

    [Header("Skill Training")]
    [Tooltip("A list of skills this character can teach the player.")]
    public List<Skill> skillsToTeach = new List<Skill>();
}