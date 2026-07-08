namespace TextEngine
{
    [System.Serializable]
    public class Response
    {
        public string responseText; // The text of the player's choice (e.g., "Ask about the key")
        public DialogueNode nextNode; // The DialogueNode this choice leads to
    }
}
