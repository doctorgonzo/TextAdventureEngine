namespace TextEngine
{
    using System.Collections.Generic;
    using UnityEngine;

    [System.Serializable]
    public class Exit
    {
        public string direction;
        public Location destination;
        public bool isLocked;
        public Item keyToUnlock;
        public string lockedDescription;
        public bool isHidden;
        public ExitActionType exitAction = ExitActionType.None;
        public string deathMessage;
        [Header("Item Block Settings")]
        public Item blockingItem;
        public string blockedMessage;
        [Header("World State Change Settings")]
        public Location targetLocation; 
        public List<Item> itemsToReveal = new List<Item>();
        public string stateChangeMessage; 
    }
}
