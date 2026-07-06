using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.SceneManagement; 
using System.IO;

public class MainMenu : MonoBehaviour
{
    public Button loadGameButton; // Drag your "Load Game" button here in the Inspector
    private const string saveFileName = "savegame.json";

    void Start()
    {
        // Check if a save file exists and disable the Load Game button if it doesn't.
        string savePath = Path.Combine(Application.persistentDataPath, saveFileName);
        if (!File.Exists(savePath))
        {
            loadGameButton.interactable = false;
        }
    }

    public void NewGame()
    {
        // Tell the GameLoader to start a new game.
        GameLoader.loadGameOnStart = false;
        // Load the main game scene.
        SceneManager.LoadScene("Main");
    }

    public void LoadGame()
    {
        // Tell the GameLoader to load the save file.
        GameLoader.loadGameOnStart = true;
        // Load the main game scene.
        SceneManager.LoadScene("Main");
    }
}