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

    [Header("Minigame")]
    [Tooltip("How well this character plays Connect Four when their dialogue starts the minigame. Easy picks random columns, Medium takes and blocks immediate wins, Hard searches ahead and plays almost perfectly.")]
    public ConnectFourDifficulty connectFourDifficulty = ConnectFourDifficulty.Medium;
}