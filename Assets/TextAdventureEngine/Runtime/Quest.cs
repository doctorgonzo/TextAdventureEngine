namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic;

    [CreateAssetMenu(menuName = "Text Adventure/Quest")]
    public class Quest : ScriptableObject
    {
        public string questName;

        [TextArea(3, 5)]
        public string questDescription;

        public List<string> objectives = new List<string>();

        [Header("Quest Rewards")]
        [Tooltip("The amount of currency the player receives upon completion.")]
        public int currencyReward;
        public int xpReward = 0;
        [Tooltip("A list of items the player receives upon completion.")]
        public List<Item> itemRewards = new List<Item>();
    }
}
