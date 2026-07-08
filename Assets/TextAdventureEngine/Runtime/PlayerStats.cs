namespace TextEngine
{
    [System.Serializable]
    public class PlayerStats
    {
        // --- Primary Attributes ---
        public int strength = 1;
        public int agility = 2;
        public int stamina = 2;
        public int intellect = 2; // For future magic/skill systems

        // --- Secondary (Calculated) Stats ---
        public int maxHealth;
        public int baseAttack;
        public float dodgeChance;
        public float hitChance;
        public int currentMana; 
        public int maxMana;

        // --- Player Progression ---
        public int level = 1;
        public int currentXp = 0;
        public int xpToNextLevel = 100;
        public int skillPoints = 0;
        public int currency = 0;
    }
}
