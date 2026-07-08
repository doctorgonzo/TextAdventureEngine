namespace TextEngine
{
    using System.Collections;
    using System.Text;
    using UnityEngine;

    // Connect Four minigame flow: board state, rendering, and player input.
    // The AI itself lives in the static ConnectFourAI class (unit-testable);
    // this partial owns the GameController-facing game loop.
    public partial class GameController
    {
        private int[,] connectFourBoard;
        private bool isPlayerTurn = true;
        // Display name and skill of whoever the minigame was started against,
        // captured from the Character asset when the game begins.
        private string minigameOpponentName = "Your opponent";
        private ConnectFourDifficulty activeMinigameDifficulty = ConnectFourDifficulty.Medium;

        void StartConnectFour()
        {
            currentGameState = GameState.MiniGame;
            connectFourBoard = ConnectFourBoard.NewBoard();
            isPlayerTurn = true; // Player always starts first in Connect Four.
            // The minigame is reached from dialogue, so the opponent is whoever
            // the player was talking to — their Character asset supplies both the
            // display name and how well they play.
            if (activeConversationCharacter != null)
            {
                minigameOpponentName = activeConversationCharacter.characterName;
                activeMinigameDifficulty = activeConversationCharacter.connectFourDifficulty;
            }
            else
            {
                minigameOpponentName = "Your opponent";
                activeMinigameDifficulty = ConnectFourDifficulty.Medium;
            }
            DisplayConnectFourBoard();
        }

        void HandleMinigameMove(string inputText)
        {
            if (!isPlayerTurn)
            {
                LogText($"Wait for {minigameOpponentName} to make a move!", TextType.GameResponse);
                return;
            }
            string formattedInput = inputText.ToLower();
            if (formattedInput == "quit" || formattedInput == "q")
            {
                EndConnectFour("You forfeit the game and step away from the board.");
                return;
            }
            // 1. Validate Input: Try to convert the input to a column number.
            if (!int.TryParse(inputText, out int columnNumber) || columnNumber < 1 || columnNumber > ConnectFourBoard.Cols)
            {
                LogText("Invalid move. Please type a column number from 1 to 7 or 'quit'");
                return;
            }
            int chosenColumn = columnNumber - 1;
            // 2. Validate Move: Check if the column is full.
            if (connectFourBoard[0, chosenColumn] != 0)
            {
                LogText("That column is full! Try another.");
                return;
            }
            // 3. Place the Piece in the lowest empty row of that column.
            int chosenRow = ConnectFourBoard.DropPiece(connectFourBoard, chosenColumn, ConnectFourBoard.PlayerId);
            // 4. Display the updated board.
            DisplayConnectFourBoard();
            if (ConnectFourBoard.CheckForWin(connectFourBoard, chosenRow, chosenColumn, ConnectFourBoard.PlayerId))
            {
                ProcessMinigameEnd(playerWon: true);
                return;
            }
            if (ConnectFourBoard.IsFull(connectFourBoard))
            {
                ProcessMinigameEnd(playerWon: false, isTie: true);
                return;
            }
            TakeAITurn();
        }

        void TakeAITurn()
        {
            isPlayerTurn = false; // Switch to AI's turn
            StartCoroutine(AITurnCoroutine());
        }

        IEnumerator AITurnCoroutine()
        {
            // 1. "Think"
            LogText($"{minigameOpponentName} is thinking...", TextType.Narrative);
            yield return new WaitForSeconds(1.0f); // Wait for 1 second
            // 2. Determine Move based on the opponent's configured difficulty.
            int chosenColumn = ConnectFourAI.ChooseColumn(connectFourBoard, activeMinigameDifficulty);
            // 3. Place Piece
            int chosenRow = ConnectFourBoard.DropPiece(connectFourBoard, chosenColumn, ConnectFourBoard.AiId);
            // 4. Display Board
            DisplayConnectFourBoard();
            // 5. Check for Win/Loss
            if (ConnectFourBoard.CheckForWin(connectFourBoard, chosenRow, chosenColumn, ConnectFourBoard.AiId))
            {
                ProcessMinigameEnd(playerWon: false);
            }
            else if (ConnectFourBoard.IsFull(connectFourBoard))
            {
                ProcessMinigameEnd(playerWon: false, isTie: true);
            }
            else
            {
                // 6. If game continues, give control back to the player.
                isPlayerTurn = true;
                LogText("Your turn.", TextType.Narrative, useTypewriter: false);
            }
        }

        void ProcessMinigameEnd(bool playerWon, bool isTie = false)
        {
            string endMessage;
            if (isTie) { endMessage = "The board is full. It's a draw!"; }
            else if (playerWon) { endMessage = "You win! Congratulations!"; }
            else { endMessage = $"{minigameOpponentName} wins! Better luck next time."; }
            // Queue the two messages. They will now appear sequentially without glitches.
            LogText(endMessage, TextType.GameResponse);
            LogText("Press Enter to continue...", TextType.Narrative);
            currentGameState = GameState.MinigamePostlude;
        }

        void EndConnectFour(string forfeitMessage = null)
        {
            currentGameState = GameState.Playing;
            if (!string.IsNullOrEmpty(forfeitMessage))
            {
                LogText(forfeitMessage, TextType.Narrative);
            }
            DisplayLocation(useTypewriter: false);
        }

        void DisplayConnectFourBoard()
        {
            StringBuilder boardText = new StringBuilder();
            for (int row = 0; row < ConnectFourBoard.Rows; row++)
            {
                boardText.Append("|");
                for (int col = 0; col < ConnectFourBoard.Cols; col++)
                {
                    switch (connectFourBoard[row, col])
                    {
                        case 0:
                            boardText.Append("<mspace=1.8em>.</mspace>");
                            break;
                        case ConnectFourBoard.PlayerId:
                            boardText.Append($"<mspace=1.8em><color={playerInputColor}>O</color></mspace>");
                            break;
                        case ConnectFourBoard.AiId:
                            boardText.Append($"<mspace=1.8em><color={enemyColor}>X</color></mspace>");
                            break;
                    }
                    boardText.Append("|");
                }
                boardText.Append("\n");
            }
            boardText.Append("-----------------------------\n");
            boardText.Append("|<mspace=1.8em>1</mspace>|<mspace=1.8em>2</mspace>|<mspace=1.8em>3</mspace>|<mspace=1.8em>4</mspace>|<mspace=1.8em>5</mspace>|<mspace=1.8em>6</mspace>|<mspace=1.8em>7</mspace>|\n");
            boardText.Append("-----------------------------");
            PrintToScreen(boardText.ToString(), useTypewriter: false);
        }
    }
}
