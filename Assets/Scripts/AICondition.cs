using UnityEngine;

[System.Serializable]
public class AICondition
{
    public AIConditionType conditionType;

    [Tooltip("For health checks, this is the percentage (e.g., 0.3 for 30%).")]
    public float floatParameter;

    [Tooltip("For status effect checks, this is the effect to look for.")]
    public StatusEffect statusEffectParameter;
}