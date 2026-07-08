namespace TextEngine
{
    // This is NOT a ScriptableObject or MonoBehaviour. It's just a data container.
    using System.Collections.Generic;

    public class ActiveQuest
    {
        public Quest quest;
        public List<bool> objectivesCompleted;

        public ActiveQuest(Quest quest)
        {
            this.quest = quest;
            // Create a list of booleans, one for each objective, and initialize them all to false.
            objectivesCompleted = new List<bool>();
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                objectivesCompleted.Add(false);
            }
        }
    }
}
