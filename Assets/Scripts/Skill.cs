using UnityEngine;
using System.Collections.Generic;

public enum SkillType
{
    Passive,
    Active
}

public enum SkillEffectType
{
    Passive_IncreaseStrength,
    Passive_IncreaseAgility,
    Passive_IncreaseStamina,
    Passive_IncreaseIntellect
    // You can add more passive effects here later
}

[CreateAssetMenu(menuName = "Text Adventure/Skill")]
public class Skill : ScriptableObject
{
    public string skillName;
    [TextArea]
    public string skillDescription;

    [Header("Requirements & Costs")]
    public int requiredLevel = 1;
    public int skillPointCost = 1;
    public int currencyCost = 100;

    [Header("Skill Type & Effects")]
    public SkillType skillType = SkillType.Passive;

    [Tooltip("For Active skills: The amount of mana this skill costs to use.")]
    public int manaCost = 10;

    [Tooltip("For Active skills: Who this skill can be targeted on.")]
    public SkillTargetType targetType = SkillTargetType.Enemy;

    [Tooltip("For Passive skills: The passive bonus this skill provides.")]
    public SkillEffectType passiveEffectType;
    [Tooltip("For Passive skills: The magnitude of the passive bonus.")]
    public int passiveEffectMagnitude;

    [Tooltip("For Active skills: The list of effects that happen when this skill is used.")]
    public List<ActiveSkillEffect> activeEffects = new List<ActiveSkillEffect>();
}
