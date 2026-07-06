using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AIAction
{
    [Tooltip("The special ability to use if the conditions are met.")]
    public SpecialAbility abilityToUse;

    [Tooltip("All of these conditions must be true for this action to be chosen.")]
    public List<AICondition> conditions = new List<AICondition>();
}