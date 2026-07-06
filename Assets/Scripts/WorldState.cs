using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns the runtime world model: the loaded ScriptableObject catalogs, the
/// per-location mutable state dictionaries, world flags, and the lookups over
/// them. Extracting this out of the MonoBehaviour makes the world state
/// reusable and unit-testable, and keeps GameController focused on game flow.
/// </summary>
public class WorldState
{
    // --- Loaded blueprint catalogs (from Resources) ---
    public Location[] allLocations;
    public Enemy[] allEnemies;
    public Item[] allItems;

    // --- Per-location runtime state ---
    public Dictionary<Location, List<Item>> roomItemsState;
    public Dictionary<Exit, bool> exitLockedState;
    public Dictionary<Exit, bool> exitVisibilityState;
    public Dictionary<Interactable, string> interactableStates;
    public Dictionary<Location, List<EnemyInstance>> roomEnemiesState;
    public Dictionary<Location, List<Character>> roomCharactersState;
    public Dictionary<Location, List<Item>> runtimeShopInventories;
    public Dictionary<Location, List<Interactable>> roomInteractablesState;
    public Dictionary<string, bool> worldFlags = new Dictionary<string, bool>();

    public WorldState()
    {
        allLocations = Resources.LoadAll<Location>("Locations");
        allEnemies = Resources.LoadAll<Enemy>("Enemies");
        allItems = Resources.LoadAll<Item>("Items");
    }

    /// <summary>
    /// (Re)builds all per-location runtime state from the location blueprints.
    /// Called when starting a new game or before applying a loaded save.
    /// Note: world flags are intentionally not reset here (matching the
    /// original behavior); the loader clears them explicitly.
    /// </summary>
    public void BuildRuntimeState()
    {
        InstantiateRoomStates();
        InstantiateExitStates();
        InstantiateRoomInteractables();
        InstantiateInteractableStates();
        InstantiateEnemyStates();
        InstantiateCharacterStates();
        InstantiateShopStates();
    }

    #region Runtime state construction
    void InstantiateRoomInteractables()
    {
        roomInteractablesState = new Dictionary<Location, List<Interactable>>();
        foreach (Location location in allLocations)
        {
            // Create a NEW list, copying the interactables from the SO's default list.
            List<Interactable> runtimeInteractables = new List<Interactable>(location.interactables);
            roomInteractablesState.Add(location, runtimeInteractables);
        }
    }

    void InstantiateShopStates()
    {
        runtimeShopInventories = new Dictionary<Location, List<Item>>();
        foreach (Location location in allLocations)
        {
            if (location.isShop)
            {
                // Create a NEW list, copying the items from the SO's default inventory.
                List<Item> runtimeInventory = new List<Item>(location.shopInventory);
                runtimeShopInventories.Add(location, runtimeInventory);
            }
        }
    }

    void InstantiateInteractableStates()
    {
        interactableStates = new Dictionary<Interactable, string>();
        foreach (Location location in allLocations)
        {
            foreach (Interactable interactable in location.interactables)
            {
                if (interactable == null)
                {
                    Debug.LogWarning($"Found a null interactable in '{location.name}', skipping.");
                    continue;
                }
                interactableStates.Add(interactable, interactable.initialState);
            }
        }
    }

    void InstantiateExitStates()
    {
        exitLockedState = new Dictionary<Exit, bool>();
        exitVisibilityState = new Dictionary<Exit, bool>(); // Initialize the new dictionary
        foreach (Location location in allLocations)
        {
            foreach (Exit exit in location.exits)
            {
                exitLockedState.Add(exit, exit.isLocked);
                exitVisibilityState.Add(exit, !exit.isHidden); // Store if it's visible (opposite of isHidden)
            }
        }
    }

    void InstantiateRoomStates()
    {
        roomItemsState = new Dictionary<Location, List<Item>>();
        foreach (Location location in allLocations)
        {
            // For each location blueprint, create a NEW list of items based on its blueprint.
            List<Item> itemsInRoom = new List<Item>(location.itemsInLocation);
            roomItemsState.Add(location, itemsInRoom);
        }
    }

    void InstantiateCharacterStates()
    {
        roomCharactersState = new Dictionary<Location, List<Character>>();
        foreach (Location location in allLocations)
        {
            // For each location blueprint, create a NEW list of characters.
            List<Character> charactersInRoom = new List<Character>(location.charactersInLocation);
            roomCharactersState.Add(location, charactersInRoom);
        }
    }

    void InstantiateEnemyStates()
    {
        roomEnemiesState = new Dictionary<Location, List<EnemyInstance>>();
        foreach (Location location in allLocations)
        {
            // Create a new list to hold the instances for this room.
            List<EnemyInstance> instancesInRoom = new List<EnemyInstance>();
            // For each enemy blueprint in the location's default list...
            foreach (Enemy enemyBlueprint in location.defaultEnemies)
            {
                // ...create a new runtime instance and add it to our list.
                instancesInRoom.Add(new EnemyInstance(enemyBlueprint));
            }
            // Add the fully populated list to the dictionary.
            roomEnemiesState.Add(location, instancesInRoom);
        }
    }
    #endregion

    #region Lookups
    public Location FindLocationByName(string name)
    {
        foreach (Location loc in allLocations) { if (loc.name == name) return loc; }
        return null;
    }

    public Item FindItemByName(string name)
    {
        if (string.IsNullOrEmpty(name) || allItems == null) return null;
        return allItems.FirstOrDefault(item => item != null && item.name == name);
    }

    public Interactable FindInteractableByNoun(string noun)
    {
        foreach (Location loc in allLocations)
        {
            foreach (Interactable interactable in loc.interactables)
            {
                if (interactable.noun == noun) return interactable;
            }
        }
        return null;
    }

    public Exit FindExit(Location loc, string direction)
    {
        if (loc == null) return null;
        foreach (Exit exit in loc.exits) { if (exit.direction == direction) return exit; }
        return null;
    }

    public Location FindLocationOfExit(Exit exitToFind)
    {
        foreach (Location loc in allLocations)
        {
            foreach (Exit exit in loc.exits) { if (exit == exitToFind) return loc; }
        }
        return null;
    }

    public Character FindCharacterByName(string name)
    {
        // bail early if we don't even have a name or any locations
        if (string.IsNullOrEmpty(name) || allLocations == null)
            return null;

        foreach (Location loc in allLocations)
        {
            // skip any null Location assets
            if (loc == null || loc.charactersInLocation == null)
                continue;

            foreach (Character character in loc.charactersInLocation)
            {
                // skip any null entries
                if (character == null)
                    continue;

                if (character.name == name)
                    return character;
            }
        }

        return null;
    }

    public Enemy FindEnemyBlueprintByName(string name)
    {
        // This now uses the pre-loaded array, which is fast and reliable.
        return allEnemies.FirstOrDefault(e => e.name == name);
    }
    #endregion
}
