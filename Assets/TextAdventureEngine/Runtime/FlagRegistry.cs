namespace TextEngine
{
    using UnityEngine;
    using System.Collections.Generic;

    [CreateAssetMenu(menuName = "Text Adventure/Flag Registry", order = 8)]
    public class FlagRegistry : ScriptableObject
    {
        [Tooltip("Define every possible flag name in your game here. This list will populate the dropdowns in other assets.")]
        public List<string> flags = new List<string>();
    }
}
