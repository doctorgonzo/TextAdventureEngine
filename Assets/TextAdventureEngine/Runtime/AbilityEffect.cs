namespace TextEngine
{
    public enum AbilityEffect
    {
        SkipTurn,
        SelfHeal,
        LifeDrain,
        StunPlayer,
        ApplyBuffToSelf,        // For positive effects on the enemy
        ApplyDebuffToPlayer,    // For negative effects on the player
        CleanseDebuffs,         // Removes negative effects from self
        DrainMana
    }
}
