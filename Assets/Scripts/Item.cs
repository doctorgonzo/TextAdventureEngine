using UnityEngine;

[CreateAssetMenu(menuName = "Text Adventure/Item")]
public class Item : ScriptableObject
{
    public string itemName;
    public AudioClip pickupSound;

    [TextArea(3, 5)]
    public string descriptionInRoom;

    [TextArea(3, 5)]
    public string description;

    public ItemType itemType = ItemType.General;

    [Header("Equipment Stats")]
    public int attackBonus = 0; // You can keep this for flat bonuses or remove it
    public int defenseBonus = 0;
    public int strengthBonus = 0; // New
    public int agilityBonus = 0;  // New
    public int staminaBonus = 0;  // New

    [Header("Consumable Stats")]
    public int healthToRestore = 0;
    public float drunkennessValue = 0.0f;

    [Header("Status Effect")]
    public StatusEffect effectToApplyOnUse; // e.g., a healing potion applies a "Regen" effect

    [Header("Economy")]
    public int buyPrice = 0;   // cost to buy from shop
    public int sellPrice = 0;   // amount you get if you sell
}