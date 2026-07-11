namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic;

    [CreateAssetMenu(menuName = "Text Adventure/Enemy Behavior", fileName = "NewEnemyBehavior", order = 6)]
    public class EnemyBehavior : ScriptableObject
    {
        [Tooltip("The enemy will evaluate these actions in order, from top to bottom. The first action whose conditions are met will be executed.")]
        public List<AIAction> prioritizedActions = new List<AIAction>();
    }
}
