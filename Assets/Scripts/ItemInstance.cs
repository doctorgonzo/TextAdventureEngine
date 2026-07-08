// Runtime instance of an Item blueprint, mirroring EnemyInstance. Items in
// rooms, shops, the player's inventory, and equipment slots are all
// ItemInstances, so per-copy state (durability, charges, enchantments) has a
// home without mutating the shared ScriptableObject asset.
public class ItemInstance
{
    public Item blueprint; // The ScriptableObject template

    public ItemInstance(Item blueprint)
    {
        this.blueprint = blueprint;
    }
}
