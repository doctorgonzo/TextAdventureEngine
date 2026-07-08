namespace TextEngine
{
    using System.IO;
    using UnityEngine;

    /// <summary>
    /// Handles persistence of a <see cref="SaveData"/> snapshot: file location,
    /// JSON serialization, and reading/writing to disk. Knows nothing about the
    /// live game state — GameController builds and applies the SaveData DTO.
    /// </summary>
    public class SaveSystem
    {
        private readonly string savePath;

        public SaveSystem(string fileName = "savegame.json")
        {
            savePath = Path.Combine(Application.persistentDataPath, fileName);
        }

        public bool SaveExists()
        {
            return File.Exists(savePath);
        }

        public void Write(SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);
        }

        /// <summary>Reads and deserializes the save file, or returns null if none exists.</summary>
        public SaveData Read()
        {
            if (!File.Exists(savePath)) return null;
            string json = File.ReadAllText(savePath);
            return JsonUtility.FromJson<SaveData>(json);
        }
    }
}
