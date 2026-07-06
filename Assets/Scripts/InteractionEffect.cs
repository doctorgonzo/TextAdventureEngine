using UnityEngine;

[System.Serializable]
public class InteractionEffect
{
    public InteractionEffectType effectType;

    [Header("Parameters")]
    [Tooltip("For LogMessage: The text to display.")]
    public string stringParameter;

    [Tooltip("For ChangeState: The new state for this interactable.")]
    public string newStateParameter;

    [Tooltip("For SetFlag: The name of the flag to set.")]
    public string flagParameter;

    [Tooltip("For SpawnItemInRoom: The Item asset to create.")]
    public Item itemParameter;

    [Tooltip("For PlaySound: The AudioClip to play.")]
    public AudioClip audioClipParameter;
}