namespace TextEngine
{
    // Values are explicit because they are serialized into SpecialAbility
    // assets by number — never renumber, only append or retire.
    public enum AbilityEffect
    {
        SkipTurn = 0,
        SelfHeal = 1,
        LifeDrain = 2,
        StunPlayer = 3,
        // 4 was ApplyBuffToSelf and 6 was CleanseDebuffs — retired: enemies
        // have no status effect system yet, so both values silently did nothing.
        ApplyDebuffToPlayer = 5,
        DrainMana = 7
    }
}
