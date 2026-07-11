namespace TextEngine
{
    // Values are explicit because they are serialized into Skill assets by
    // number — never renumber, only append or retire.
    public enum ActiveSkillEffectType
    {
        DealDamage = 0,
        HealSelf = 1,
        // 2 was ApplyStatusEffectToTarget — retired: enemies have no status
        // effect system yet, so the value silently did nothing.
        ApplyStatusEffectToSelf = 3
    }
}
