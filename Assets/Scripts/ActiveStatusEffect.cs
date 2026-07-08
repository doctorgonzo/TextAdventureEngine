public class ActiveStatusEffect
{
    public StatusEffect effect;
    private float remainingTime;
    private float tickTimer;

    // Expose for UI
    public float RemainingTime => remainingTime;

    public ActiveStatusEffect(StatusEffect effect)
    {
        this.effect = effect;
        this.remainingTime = effect.durationInSeconds;
        this.tickTimer = effect.tickInterval;
    }

    // Used when restoring a saved game: resumes with the saved remaining time
    // instead of the effect's full duration.
    public ActiveStatusEffect(StatusEffect effect, float remainingTime)
    {
        this.effect = effect;
        this.remainingTime = remainingTime;
        this.tickTimer = effect.tickInterval;
    }

    public bool Tick(float deltaTime, GameController controller)
    {
        remainingTime -= deltaTime;
        tickTimer -= deltaTime;

        if (tickTimer <= 0f)
        {
            tickTimer += effect.tickInterval;
            controller.ApplyTimedEffect(effect);
        }

        return remainingTime <= 0f;
    }
}
