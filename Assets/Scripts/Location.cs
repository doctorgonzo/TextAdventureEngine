using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Text Adventure/Location")]
public class Location : ScriptableObject
{
    [TextArea(10, 15)]
    public string description;
    public Exit[] exits;
    public List<Item> itemsInLocation = new List<Item>();
    public List<Interactable> interactables = new List<Interactable>();
    public List<Character> charactersInLocation = new List<Character>();
    public AudioClip backgroundMusic;
    [Header("Default Spawns")]
    [Tooltip("Enemies that will be present in this location when a new game starts.")]
    public List<Enemy> defaultEnemies = new List<Enemy>(); // Add this line
    [Header("Shop Settings")]
    public bool isShop = false;               
    public List<Item> shopInventory = new List<Item>(); 
    [Header("Editor")]
    public Vector2 editorPosition;
}