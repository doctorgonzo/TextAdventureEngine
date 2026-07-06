public enum InteractionEffectType
{
    LogMessage,       // Displays text to the player
    ChangeState,      // Changes the interactable's own state
    SetFlag,          // Sets a world flag to true
    RevealExit,       // Makes a hidden exit visible
    SpawnItemInRoom,  // Adds a new item to the current location
    PlaySound,         // Plays a one-off audio clip
    ConsumeUsedItem
}
