namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic; 

    [CreateAssetMenu(menuName = "Text Adventure/Action")]
    public class Action : ScriptableObject
    {
        public string keyword; // The main verb we'll use in our code (e.g., "go")

        // A list of other words the player might type for this action.
        public List<string> synonyms = new List<string>();
    }
}
