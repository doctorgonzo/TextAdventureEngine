using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TextType { Narrative, PlayerInput, GameResponse }

public struct TextBlock
{
    public string text;
    public TextType type;
    public bool clearScreen;
    public float delay;
    public bool useTypewriter;
}

/// <summary>
/// Owns the on-screen text output pipeline: the queued typewriter effect,
/// colorization, scrolling, and input-field enable/focus while text streams.
/// This is a plain class (not a MonoBehaviour) so GameController keeps its
/// existing inspector wiring and simply injects the references it already has.
/// </summary>
public class TextRenderer
{
    private readonly MonoBehaviour host; // used to run coroutines
    private readonly TextMeshProUGUI displayText;
    private readonly TMP_InputField inputField;
    private readonly ScrollRect scrollRect;
    private readonly float charactersPerSecond;
    private readonly Func<string, string> preprocess; // e.g. player-name substitution
    private readonly string playerInputColor;
    private readonly string gameResponseColor;

    private readonly Queue<TextBlock> textQueue = new Queue<TextBlock>();
    private bool isProcessingQueue = false;
    private Coroutine processingCoroutine;

    public bool IsProcessing => isProcessingQueue;

    public TextRenderer(
        MonoBehaviour host,
        TextMeshProUGUI displayText,
        TMP_InputField inputField,
        ScrollRect scrollRect,
        float charactersPerSecond,
        Func<string, string> preprocess,
        string playerInputColor,
        string gameResponseColor)
    {
        this.host = host;
        this.displayText = displayText;
        this.inputField = inputField;
        this.scrollRect = scrollRect;
        this.charactersPerSecond = charactersPerSecond;
        this.preprocess = preprocess ?? (s => s);
        this.playerInputColor = playerInputColor;
        this.gameResponseColor = gameResponseColor;
    }

    /// <summary>Queue text that clears the screen before printing.</summary>
    public void Print(string text, bool useTypewriter = true)
    {
        textQueue.Enqueue(new TextBlock { text = text, type = TextType.Narrative, clearScreen = true, useTypewriter = useTypewriter });
        StartProcessing();
    }

    /// <summary>Queue text that is appended below whatever is already on screen.</summary>
    public void Log(string text, TextType type = TextType.GameResponse, bool useTypewriter = true)
    {
        textQueue.Enqueue(new TextBlock { text = text, type = type, clearScreen = false, useTypewriter = useTypewriter });
        StartProcessing();
    }

    /// <summary>
    /// Append (or replace) text instantly, bypassing the queue and typewriter.
    /// Used for echoing the player's command so it feels responsive.
    /// </summary>
    public void ShowImmediate(string rawText, TextType type, bool clear = false)
    {
        string colorized = Colorize(preprocess(rawText), type);
        if (clear || string.IsNullOrEmpty(displayText.text))
        {
            displayText.text = colorized;
        }
        else
        {
            displayText.text += "\n\n" + colorized;
        }
        displayText.maxVisibleCharacters = int.MaxValue;
        host.StartCoroutine(ScrollToBottom());
    }

    /// <summary>
    /// Instantly finish the currently streaming text and flush the rest of the
    /// queue. Only stops the text coroutine, never other game coroutines.
    /// </summary>
    public void SkipToEnd()
    {
        if (processingCoroutine != null) host.StopCoroutine(processingCoroutine);
        processingCoroutine = null;

        displayText.maxVisibleCharacters = int.MaxValue;
        while (textQueue.Count > 0)
        {
            TextBlock block = textQueue.Dequeue();
            if (block.delay > 0) continue; // process delays instantly by skipping them
            string fullText = Colorize(preprocess(block.text), block.type);
            if (block.clearScreen)
            {
                displayText.text = fullText;
            }
            else
            {
                displayText.text += "\n\n" + fullText;
            }
        }

        isProcessingQueue = false;
        inputField.interactable = true;
        inputField.ActivateInputField();
        host.StartCoroutine(ScrollToBottom());
    }

    private void StartProcessing()
    {
        if (isProcessingQueue) return;
        processingCoroutine = host.StartCoroutine(ProcessQueueCoroutine());
    }

    private IEnumerator ProcessQueueCoroutine()
    {
        isProcessingQueue = true;
        inputField.interactable = false;
        while (textQueue.Count > 0)
        {
            TextBlock currentBlock = textQueue.Dequeue();
            if (currentBlock.delay > 0) { yield return new WaitForSeconds(currentBlock.delay); }
            if (!string.IsNullOrEmpty(currentBlock.text))
            {
                string processedText = preprocess(currentBlock.text);
                string colorizedText = Colorize(processedText, currentBlock.type);
                int startChar;
                if (currentBlock.clearScreen)
                {
                    displayText.text = colorizedText;
                    startChar = 0;
                }
                else
                {
                    displayText.maxVisibleCharacters = int.MaxValue;
                    startChar = displayText.text.Length;
                    displayText.text += "\n\n" + colorizedText;
                }
                if (currentBlock.useTypewriter)
                {
                    int totalLength = displayText.text.Length;
                    if (startChar < 0) startChar = 0;
                    float delay = 1f / charactersPerSecond;
                    for (int i = startChar; i < totalLength; i++)
                    {
                        displayText.maxVisibleCharacters = i + 1;
                        yield return new WaitForSeconds(delay);
                    }
                }
                else
                {
                    displayText.maxVisibleCharacters = int.MaxValue;
                }
                host.StartCoroutine(ScrollToBottom());
            }
        }
        isProcessingQueue = false;
        inputField.interactable = true;
        inputField.ActivateInputField();
    }

    private string Colorize(string text, TextType type)
    {
        switch (type)
        {
            case TextType.PlayerInput:
                return $"<color={playerInputColor}>{text}</color>";
            case TextType.GameResponse:
                return $"<color={gameResponseColor}>{text}</color>";
            case TextType.Narrative:
            default:
                return text;
        }
    }

    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}
