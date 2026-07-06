using UnityEngine;

public class GameLoader : MonoBehaviour
{
    public static bool loadGameOnStart = false;

    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }
}
