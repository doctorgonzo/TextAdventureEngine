using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// How well the Connect Four opponent plays. Selected on the GameController
// in the inspector.
public enum ConnectFourDifficulty { Easy, Medium, Hard }

// Connect Four minigame: board state, rendering, player input, and the AI
// opponent. This is a partial of the same class defined in GameController.cs.
public partial class GameController
{
    [Header("Minigame — Connect Four")]
    [Tooltip("How well the Connect Four opponent plays. Easy picks random columns, Medium takes and blocks immediate wins, Hard searches ahead and plays almost perfectly.")]
    public ConnectFourDifficulty connectFourDifficulty = ConnectFourDifficulty.Medium;

    private const int C4_ROWS = 6;
    private const int C4_COLS = 7;
    private const int C4_PLAYER = 1;
    private const int C4_AI = 2;
    // Hard-mode look-ahead in plies (half-moves). 8 gives near-perfect casual
    // play; with alpha-beta pruning it resolves in well under a frame.
    private const int C4_HARD_SEARCH_DEPTH = 8;
    // Columns ordered center-out. Searching stronger moves first makes
    // alpha-beta pruning far more effective, and doubles as a preference
    // list when moves score equally.
    private static readonly int[] c4ColumnOrder = { 3, 2, 4, 1, 5, 0, 6 };

    private int[,] connectFourBoard;
    private bool isPlayerTurn = true;
    // Display name of whoever the minigame was started against.
    private string minigameOpponentName = "Your opponent";

    #region Game flow
    void StartConnectFour()
    {
        currentGameState = GameState.MiniGame;
        connectFourBoard = new int[C4_ROWS, C4_COLS];
        isPlayerTurn = true; // Player always starts first in Connect Four.
        // The minigame is reached from dialogue, so the opponent is whoever
        // the player was talking to.
        minigameOpponentName = activeConversationCharacter != null
            ? activeConversationCharacter.characterName
            : "Your opponent";
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
        if (!int.TryParse(inputText, out int columnNumber) || columnNumber < 1 || columnNumber > C4_COLS)
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
        int chosenRow = DropPiece(chosenColumn, C4_PLAYER);
        // 4. Display the updated board.
        DisplayConnectFourBoard();
        if (CheckForWin(chosenRow, chosenColumn, C4_PLAYER))
        {
            ProcessMinigameEnd(playerWon: true);
            return;
        }
        if (CheckForTie())
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
        // 2. Determine Move based on the configured difficulty.
        int chosenColumn = ChooseAIColumn();
        // 3. Place Piece
        int chosenRow = DropPiece(chosenColumn, C4_AI);
        // 4. Display Board
        DisplayConnectFourBoard();
        // 5. Check for Win/Loss
        if (CheckForWin(chosenRow, chosenColumn, C4_AI))
        {
            ProcessMinigameEnd(playerWon: false);
        }
        else if (CheckForTie())
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
    #endregion

    #region AI move selection
    private int ChooseAIColumn()
    {
        List<int> validColumns = GetValidColumns();
        switch (connectFourDifficulty)
        {
            case ConnectFourDifficulty.Hard:
                return ChooseHardColumn(validColumns);
            case ConnectFourDifficulty.Medium:
                return ChooseMediumColumn(validColumns);
            default:
                return validColumns[Random.Range(0, validColumns.Count)];
        }
    }

    // Medium: take an immediate win, block the player's immediate win,
    // otherwise play randomly with a preference for central columns.
    private int ChooseMediumColumn(List<int> validColumns)
    {
        int winningColumn = FindImmediateWin(C4_AI, validColumns);
        if (winningColumn != -1) return winningColumn;
        int blockingColumn = FindImmediateWin(C4_PLAYER, validColumns);
        if (blockingColumn != -1) return blockingColumn;
        // Weighted random: a column's weight falls off with distance from
        // the center, so medium play looks purposeful without reading ahead.
        List<int> weighted = new List<int>();
        foreach (int col in validColumns)
        {
            int weight = 4 - Mathf.Abs(col - C4_COLS / 2);
            for (int i = 0; i < weight; i++) weighted.Add(col);
        }
        return weighted[Random.Range(0, weighted.Count)];
    }

    // Returns a column where dropping a piece for 'id' wins on the spot, or -1.
    private int FindImmediateWin(int id, List<int> validColumns)
    {
        foreach (int col in validColumns)
        {
            int row = DropPiece(col, id);
            bool wins = CheckForWin(row, col, id);
            UndoPiece(row, col);
            if (wins) return col;
        }
        return -1;
    }

    // Hard: minimax with alpha-beta pruning over C4_HARD_SEARCH_DEPTH plies.
    // Ties between equally strong moves are broken randomly so games vary.
    private int ChooseHardColumn(List<int> validColumns)
    {
        int bestScore = int.MinValue;
        List<int> bestColumns = new List<int>();
        foreach (int col in c4ColumnOrder)
        {
            if (!validColumns.Contains(col)) continue;
            int row = DropPiece(col, C4_AI);
            int score = CheckForWin(row, col, C4_AI)
                ? WinScore(C4_HARD_SEARCH_DEPTH)
                : Minimax(C4_HARD_SEARCH_DEPTH - 1, int.MinValue, int.MaxValue, maximizing: false);
            UndoPiece(row, col);
            if (score > bestScore)
            {
                bestScore = score;
                bestColumns.Clear();
                bestColumns.Add(col);
            }
            else if (score == bestScore)
            {
                bestColumns.Add(col);
            }
        }
        // If every line is a forced loss the scores are all equal, and pure
        // minimax has no reason to prefer any move. Block the player's
        // immediate win anyway — going down fighting looks a lot smarter than
        // ignoring the threat, and a human opponent might still misplay.
        if (bestScore <= -1000000)
        {
            int blockingColumn = FindImmediateWin(C4_PLAYER, validColumns);
            if (blockingColumn != -1) return blockingColumn;
        }
        return bestColumns[Random.Range(0, bestColumns.Count)];
    }

    // Classic minimax with alpha-beta pruning, mutating connectFourBoard in
    // place (drop, recurse, undo). Positive scores favor the AI.
    private int Minimax(int depth, int alpha, int beta, bool maximizing)
    {
        List<int> validColumns = GetValidColumns();
        if (validColumns.Count == 0) return 0; // full board: draw
        if (depth == 0) return EvaluateBoard();

        int id = maximizing ? C4_AI : C4_PLAYER;
        int best = maximizing ? int.MinValue : int.MaxValue;
        foreach (int col in c4ColumnOrder)
        {
            if (!validColumns.Contains(col)) continue;
            int row = DropPiece(col, id);
            int score;
            if (CheckForWin(row, col, id))
            {
                // Depth-adjusted terminal scores make the AI prefer faster
                // wins and drag out unavoidable losses.
                score = maximizing ? WinScore(depth) : -WinScore(depth);
            }
            else
            {
                score = Minimax(depth - 1, alpha, beta, !maximizing);
            }
            UndoPiece(row, col);
            if (maximizing)
            {
                if (score > best) best = score;
                if (best > alpha) alpha = best;
            }
            else
            {
                if (score < best) best = score;
                if (best < beta) beta = best;
            }
            if (alpha >= beta) break; // prune: opponent won't allow this line
        }
        return best;
    }

    // A terminal win found with more remaining depth happens sooner.
    private static int WinScore(int depthRemaining) => 1000000 + depthRemaining;

    // Heuristic for non-terminal positions: score every 4-cell window
    // (horizontal, vertical, both diagonals) plus a small bonus for center
    // column control.
    private int EvaluateBoard()
    {
        int score = 0;
        for (int r = 0; r < C4_ROWS; r++)
        {
            if (connectFourBoard[r, C4_COLS / 2] == C4_AI) score += 3;
            else if (connectFourBoard[r, C4_COLS / 2] == C4_PLAYER) score -= 3;
        }
        for (int r = 0; r < C4_ROWS; r++)
        {
            for (int c = 0; c < C4_COLS; c++)
            {
                if (c + 3 < C4_COLS) score += ScoreWindow(r, c, 0, 1);                 // right
                if (r + 3 < C4_ROWS) score += ScoreWindow(r, c, 1, 0);                 // down
                if (r + 3 < C4_ROWS && c + 3 < C4_COLS) score += ScoreWindow(r, c, 1, 1);  // down-right
                if (r + 3 < C4_ROWS && c - 3 >= 0) score += ScoreWindow(r, c, 1, -1);      // down-left
            }
        }
        return score;
    }

    // Scores one 4-cell window. A window containing both colors can never be
    // completed and is worth nothing.
    private int ScoreWindow(int row, int col, int dr, int dc)
    {
        int ai = 0, player = 0;
        for (int i = 0; i < 4; i++)
        {
            int cell = connectFourBoard[row + dr * i, col + dc * i];
            if (cell == C4_AI) ai++;
            else if (cell == C4_PLAYER) player++;
        }
        if (ai > 0 && player > 0) return 0;
        if (ai == 3) return 50;
        if (ai == 2) return 10;
        if (player == 3) return -60; // weight the player's threats slightly higher
        if (player == 2) return -10;
        return 0;
    }
    #endregion

    #region Board helpers
    private List<int> GetValidColumns()
    {
        var valid = new List<int>();
        for (int col = 0; col < C4_COLS; col++)
        {
            if (connectFourBoard[0, col] == 0) valid.Add(col);
        }
        return valid;
    }

    // Drops a piece into the lowest empty row of the column and returns that
    // row. Callers guarantee the column isn't full.
    private int DropPiece(int col, int id)
    {
        for (int row = C4_ROWS - 1; row >= 0; row--)
        {
            if (connectFourBoard[row, col] == 0)
            {
                connectFourBoard[row, col] = id;
                return row;
            }
        }
        return -1;
    }

    private void UndoPiece(int row, int col)
    {
        connectFourBoard[row, col] = 0;
    }

    private bool CheckForWin(int row, int col, int playerID)
    {
        // Check Horizontal
        int count = 0;
        for (int c = 0; c < C4_COLS; c++)
        {
            if (connectFourBoard[row, c] == playerID) count++;
            else count = 0;
            if (count >= 4) return true;
        }
        // Check Vertical
        count = 0;
        for (int r = 0; r < C4_ROWS; r++)
        {
            if (connectFourBoard[r, col] == playerID) count++;
            else count = 0;
            if (count >= 4) return true;
        }
        // Check Diagonal (down-right)
        count = 0;
        for (int r = row, c = col; r < C4_ROWS && c < C4_COLS; r++, c++)
        {
            if (connectFourBoard[r, c] == playerID) count++;
            else break;
        }
        for (int r = row - 1, c = col - 1; r >= 0 && c >= 0; r--, c--)
        {
            if (connectFourBoard[r, c] == playerID) count++;
            else break;
        }
        if (count >= 4) return true;
        // Check Diagonal (up-right)
        count = 0;
        for (int r = row, c = col; r >= 0 && c < C4_COLS; r--, c++)
        {
            if (connectFourBoard[r, c] == playerID) count++;
            else break;
        }
        for (int r = row + 1, c = col - 1; r < C4_ROWS && c >= 0; r++, c--)
        {
            if (connectFourBoard[r, c] == playerID) count++;
            else break;
        }
        if (count >= 4) return true;
        return false; // No win found
    }

    private bool CheckForTie()
    {
        // A tie occurs if the top row is completely full.
        for (int c = 0; c < C4_COLS; c++)
        {
            if (connectFourBoard[0, c] == 0) // If any spot in the top row is empty...
            {
                return false; // ...it's not a tie yet.
            }
        }
        return true; // The top row is full, so the board is full.
    }

    void DisplayConnectFourBoard()
    {
        StringBuilder boardText = new StringBuilder();
        for (int row = 0; row < C4_ROWS; row++)
        {
            boardText.Append("|");
            for (int col = 0; col < C4_COLS; col++)
            {
                switch (connectFourBoard[row, col])
                {
                    case 0:
                        boardText.Append("<mspace=1.8em>.</mspace>");
                        break;
                    case C4_PLAYER:
                        boardText.Append($"<mspace=1.8em><color={playerInputColor}>O</color></mspace>");
                        break;
                    case C4_AI:
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
    #endregion
}
