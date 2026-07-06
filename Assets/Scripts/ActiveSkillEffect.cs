using UnityEngine;

[System.Serializable]
public class ActiveSkillEffect
{
    public ActiveSkillEffectType effectType;

    [Header("Parameters")]
    [Tooltip("For DealDamage or HealSelf: The amount of damage/healing.")]
    public int intParameter;

    [Tooltip("For ApplyStatusEffect: The effect to apply.")]
    public StatusEffect statusEffectParameter;
}