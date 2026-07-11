namespace TextEngine
{
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(menuName = "Text Adventure/Enemy", order = 5)]
    public class Enemy : ScriptableObject
    {
        public string enemyName;

        [TextArea(3, 5)]
        public string description;

        [TextArea(3, 5)]
        public string detailedDescription;
        public int xpReward = 10;

        [Header("Combat Stats")]
        public int maxHealth;
        public int baseAttack;
        public int damageVariance;
        [Range(0, 1)]
        public float evasionChance = 0.1f;

        [Header("Behavior & Abilities")]
        [Tooltip("The AI 'brain' that this enemy will use in combat.")]
        public EnemyBehavior behavior; // Replaces the old special abilities list
        [Tooltip("If true, this enemy will attack the player as soon as they enter the room.")]
        public bool attacksOnSight = false;
        [Header("Loot & Death")]
        public List<Item> lootDrops = new List<Item>();
        public string exitDirectionToReveal;
    }
}
