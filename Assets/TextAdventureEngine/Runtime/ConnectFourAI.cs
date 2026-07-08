namespace TextEngine
{
    using System.Collections.Generic;
    using UnityEngine;

    // How well a Connect Four opponent plays. Set per NPC on the Character
    // asset, so different characters can offer easy, medium, or hard games.
    public enum ConnectFourDifficulty { Easy, Medium, Hard }

    /// <summary>
    /// Pure board helpers for the Connect Four minigame. Cell values are 0
    /// (empty), <see cref="PlayerId"/>, or <see cref="AiId"/>. Free of any
    /// GameController state so the logic is unit-testable.
    /// </summary>
    public static class ConnectFourBoard
    {
        public const int Rows = 6;
        public const int Cols = 7;
        public const int PlayerId = 1;
        public const int AiId = 2;

        public static int[,] NewBoard() => new int[Rows, Cols];

        public static List<int> GetValidColumns(int[,] board)
        {
            var valid = new List<int>();
            for (int col = 0; col < Cols; col++)
            {
                if (board[0, col] == 0) valid.Add(col);
            }
            return valid;
        }

        /// <summary>
        /// Drops a piece into the lowest empty row of the column and returns
        /// that row. Callers guarantee the column isn't full.
        /// </summary>
        public static int DropPiece(int[,] board, int col, int id)
        {
            for (int row = Rows - 1; row >= 0; row--)
            {
                if (board[row, col] == 0)
                {
                    board[row, col] = id;
                    return row;
                }
            }
            return -1;
        }

        public static void UndoPiece(int[,] board, int row, int col)
        {
            board[row, col] = 0;
        }

        /// <summary>Checks whether the piece at (row, col) completes four in a row for playerID.</summary>
        public static bool CheckForWin(int[,] board, int row, int col, int playerID)
        {
            // Check Horizontal
            int count = 0;
            for (int c = 0; c < Cols; c++)
            {
                if (board[row, c] == playerID) count++;
                else count = 0;
                if (count >= 4) return true;
            }
            // Check Vertical
            count = 0;
            for (int r = 0; r < Rows; r++)
            {
                if (board[r, col] == playerID) count++;
                else count = 0;
                if (count >= 4) return true;
            }
            // Check Diagonal (down-right)
            count = 0;
            for (int r = row, c = col; r < Rows && c < Cols; r++, c++)
            {
                if (board[r, c] == playerID) count++;
                else break;
            }
            for (int r = row - 1, c = col - 1; r >= 0 && c >= 0; r--, c--)
            {
                if (board[r, c] == playerID) count++;
                else break;
            }
            if (count >= 4) return true;
            // Check Diagonal (up-right)
            count = 0;
            for (int r = row, c = col; r >= 0 && c < Cols; r--, c++)
            {
                if (board[r, c] == playerID) count++;
                else break;
            }
            for (int r = row + 1, c = col - 1; r < Rows && c >= 0; r++, c--)
            {
                if (board[r, c] == playerID) count++;
                else break;
            }
            if (count >= 4) return true;
            return false; // No win found
        }

        /// <summary>The board is full (a draw) when no cell in the top row is empty.</summary>
        public static bool IsFull(int[,] board)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (board[0, c] == 0) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The Connect Four opponent. Easy picks random valid columns, Medium
    /// takes and blocks immediate wins, Hard runs minimax with alpha-beta
    /// pruning and plays almost perfectly. Pure and stateless (aside from
    /// UnityEngine.Random) so it is unit-testable; covered by the EditMode
    /// test suite.
    /// </summary>
    public static class ConnectFourAI
    {
        // Hard-mode look-ahead in plies (half-moves). 8 gives near-perfect
        // casual play; with alpha-beta pruning it resolves in well under a
        // frame.
        private const int HardSearchDepth = 8;
        // Columns ordered center-out. Searching stronger moves first makes
        // alpha-beta pruning far more effective, and doubles as a preference
        // list when moves score equally.
        private static readonly int[] columnOrder = { 3, 2, 4, 1, 5, 0, 6 };

        /// <summary>Picks the AI's next column for the given board and difficulty.</summary>
        public static int ChooseColumn(int[,] board, ConnectFourDifficulty difficulty)
        {
            List<int> validColumns = ConnectFourBoard.GetValidColumns(board);
            switch (difficulty)
            {
                case ConnectFourDifficulty.Hard:
                    return ChooseHardColumn(board, validColumns);
                case ConnectFourDifficulty.Medium:
                    return ChooseMediumColumn(board, validColumns);
                default:
                    return validColumns[Random.Range(0, validColumns.Count)];
            }
        }

        // Medium: take an immediate win, block the player's immediate win,
        // otherwise play randomly with a preference for central columns.
        private static int ChooseMediumColumn(int[,] board, List<int> validColumns)
        {
            int winningColumn = FindImmediateWin(board, ConnectFourBoard.AiId, validColumns);
            if (winningColumn != -1) return winningColumn;
            int blockingColumn = FindImmediateWin(board, ConnectFourBoard.PlayerId, validColumns);
            if (blockingColumn != -1) return blockingColumn;
            // Weighted random: a column's weight falls off with distance from
            // the center, so medium play looks purposeful without reading ahead.
            List<int> weighted = new List<int>();
            foreach (int col in validColumns)
            {
                int weight = 4 - Mathf.Abs(col - ConnectFourBoard.Cols / 2);
                for (int i = 0; i < weight; i++) weighted.Add(col);
            }
            return weighted[Random.Range(0, weighted.Count)];
        }

        // Returns a column where dropping a piece for 'id' wins on the spot, or -1.
        private static int FindImmediateWin(int[,] board, int id, List<int> validColumns)
        {
            foreach (int col in validColumns)
            {
                int row = ConnectFourBoard.DropPiece(board, col, id);
                bool wins = ConnectFourBoard.CheckForWin(board, row, col, id);
                ConnectFourBoard.UndoPiece(board, row, col);
                if (wins) return col;
            }
            return -1;
        }

        // Hard: minimax with alpha-beta pruning over HardSearchDepth plies.
        // Ties between equally strong moves are broken randomly so games vary.
        private static int ChooseHardColumn(int[,] board, List<int> validColumns)
        {
            int bestScore = int.MinValue;
            List<int> bestColumns = new List<int>();
            foreach (int col in columnOrder)
            {
                if (!validColumns.Contains(col)) continue;
                int row = ConnectFourBoard.DropPiece(board, col, ConnectFourBoard.AiId);
                int score = ConnectFourBoard.CheckForWin(board, row, col, ConnectFourBoard.AiId)
                    ? WinScore(HardSearchDepth)
                    : Minimax(board, HardSearchDepth - 1, int.MinValue, int.MaxValue, maximizing: false);
                ConnectFourBoard.UndoPiece(board, row, col);
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
            // immediate win anyway — going down fighting looks a lot smarter
            // than ignoring the threat, and a human might still misplay.
            if (bestScore <= -1000000)
            {
                int blockingColumn = FindImmediateWin(board, ConnectFourBoard.PlayerId, validColumns);
                if (blockingColumn != -1) return blockingColumn;
            }
            return bestColumns[Random.Range(0, bestColumns.Count)];
        }

        // Classic minimax with alpha-beta pruning, mutating the board in
        // place (drop, recurse, undo). Positive scores favor the AI.
        private static int Minimax(int[,] board, int depth, int alpha, int beta, bool maximizing)
        {
            List<int> validColumns = ConnectFourBoard.GetValidColumns(board);
            if (validColumns.Count == 0) return 0; // full board: draw
            if (depth == 0) return EvaluateBoard(board);

            int id = maximizing ? ConnectFourBoard.AiId : ConnectFourBoard.PlayerId;
            int best = maximizing ? int.MinValue : int.MaxValue;
            foreach (int col in columnOrder)
            {
                if (!validColumns.Contains(col)) continue;
                int row = ConnectFourBoard.DropPiece(board, col, id);
                int score;
                if (ConnectFourBoard.CheckForWin(board, row, col, id))
                {
                    // Depth-adjusted terminal scores make the AI prefer faster
                    // wins and drag out unavoidable losses.
                    score = maximizing ? WinScore(depth) : -WinScore(depth);
                }
                else
                {
                    score = Minimax(board, depth - 1, alpha, beta, !maximizing);
                }
                ConnectFourBoard.UndoPiece(board, row, col);
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
        private static int EvaluateBoard(int[,] board)
        {
            int score = 0;
            for (int r = 0; r < ConnectFourBoard.Rows; r++)
            {
                if (board[r, ConnectFourBoard.Cols / 2] == ConnectFourBoard.AiId) score += 3;
                else if (board[r, ConnectFourBoard.Cols / 2] == ConnectFourBoard.PlayerId) score -= 3;
            }
            for (int r = 0; r < ConnectFourBoard.Rows; r++)
            {
                for (int c = 0; c < ConnectFourBoard.Cols; c++)
                {
                    if (c + 3 < ConnectFourBoard.Cols) score += ScoreWindow(board, r, c, 0, 1);                              // right
                    if (r + 3 < ConnectFourBoard.Rows) score += ScoreWindow(board, r, c, 1, 0);                              // down
                    if (r + 3 < ConnectFourBoard.Rows && c + 3 < ConnectFourBoard.Cols) score += ScoreWindow(board, r, c, 1, 1);  // down-right
                    if (r + 3 < ConnectFourBoard.Rows && c - 3 >= 0) score += ScoreWindow(board, r, c, 1, -1);                    // down-left
                }
            }
            return score;
        }

        // Scores one 4-cell window. A window containing both colors can never
        // be completed and is worth nothing.
        private static int ScoreWindow(int[,] board, int row, int col, int dr, int dc)
        {
            int ai = 0, player = 0;
            for (int i = 0; i < 4; i++)
            {
                int cell = board[row + dr * i, col + dc * i];
                if (cell == ConnectFourBoard.AiId) ai++;
                else if (cell == ConnectFourBoard.PlayerId) player++;
            }
            if (ai > 0 && player > 0) return 0;
            if (ai == 3) return 50;
            if (ai == 2) return 10;
            if (player == 3) return -60; // weight the player's threats slightly higher
            if (player == 2) return -10;
            return 0;
        }
    }
}
