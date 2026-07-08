namespace TextEngine.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using TextEngine;
    using Random = UnityEngine.Random;

    public class ConnectFourAITests
    {
        [SetUp]
        public void SeedRandom()
        {
            // Deterministic tie-breaks so these tests never flake.
            Random.InitState(12345);
        }

        private static int[,] BoardWith(params (int col, int id)[] drops)
        {
            var board = ConnectFourBoard.NewBoard();
            foreach (var (col, id) in drops)
            {
                ConnectFourBoard.DropPiece(board, col, id);
            }
            return board;
        }

        [Test]
        public void MediumAndHard_TakeAnImmediateWin()
        {
            // AI has three in a row on the bottom at columns 0-2: column 3 wins.
            foreach (var difficulty in new[] { ConnectFourDifficulty.Medium, ConnectFourDifficulty.Hard })
            {
                var board = BoardWith((0, ConnectFourBoard.AiId), (1, ConnectFourBoard.AiId), (2, ConnectFourBoard.AiId),
                                      (6, ConnectFourBoard.PlayerId), (5, ConnectFourBoard.PlayerId));
                Assert.AreEqual(3, ConnectFourAI.ChooseColumn(board, difficulty), $"{difficulty} must take the winning move");
            }
        }

        [Test]
        public void MediumAndHard_BlockASingleThreat()
        {
            // Player has three in a row at columns 0-2 (edge-bounded): only column 3 saves the game.
            foreach (var difficulty in new[] { ConnectFourDifficulty.Medium, ConnectFourDifficulty.Hard })
            {
                var board = BoardWith((0, ConnectFourBoard.PlayerId), (1, ConnectFourBoard.PlayerId), (2, ConnectFourBoard.PlayerId),
                                      (6, ConnectFourBoard.AiId), (6, ConnectFourBoard.AiId));
                Assert.AreEqual(3, ConnectFourAI.ChooseColumn(board, difficulty), $"{difficulty} must block the threat");
            }
        }

        [Test]
        public void MediumAndHard_BlockAVerticalStack()
        {
            foreach (var difficulty in new[] { ConnectFourDifficulty.Medium, ConnectFourDifficulty.Hard })
            {
                var board = BoardWith((6, ConnectFourBoard.PlayerId), (6, ConnectFourBoard.PlayerId), (6, ConnectFourBoard.PlayerId),
                                      (0, ConnectFourBoard.AiId), (1, ConnectFourBoard.AiId));
                Assert.AreEqual(6, ConnectFourAI.ChooseColumn(board, difficulty), $"{difficulty} must block the stack");
            }
        }

        [Test]
        public void Hard_StillBlocksInALostPosition()
        {
            // Open-ended three (columns 2-4): threats at 1 and 5 — a forced loss,
            // but Hard should still block one side rather than shrug.
            var board = BoardWith((2, ConnectFourBoard.PlayerId), (3, ConnectFourBoard.PlayerId), (4, ConnectFourBoard.PlayerId),
                                  (0, ConnectFourBoard.AiId), (0, ConnectFourBoard.AiId));
            int chosen = ConnectFourAI.ChooseColumn(board, ConnectFourDifficulty.Hard);
            Assert.That(chosen == 1 || chosen == 5, $"Hard should block one side of the double threat, chose {chosen}");
        }

        [Test]
        public void AllDifficulties_OnlyReturnValidColumns()
        {
            // Fill most of the board and confirm every difficulty picks a legal move.
            var board = ConnectFourBoard.NewBoard();
            // Fill columns 0-4 completely with alternating pieces.
            for (int col = 0; col <= 4; col++)
            {
                for (int i = 0; i < ConnectFourBoard.Rows; i++)
                {
                    ConnectFourBoard.DropPiece(board, col, (i % 2 == 0) ? ConnectFourBoard.PlayerId : ConnectFourBoard.AiId);
                }
            }
            var valid = ConnectFourBoard.GetValidColumns(board);
            foreach (var difficulty in new[] { ConnectFourDifficulty.Easy, ConnectFourDifficulty.Medium, ConnectFourDifficulty.Hard })
            {
                int chosen = ConnectFourAI.ChooseColumn(board, difficulty);
                Assert.Contains(chosen, valid, $"{difficulty} picked a full column");
            }
        }

        [Test]
        public void Hard_BeatsRandomFromTheSecondSeat()
        {
            // The random "player" moves first (as in the real game); Hard plays
            // second and should win every game despite the seat disadvantage.
            for (int game = 0; game < 5; game++)
            {
                Assert.AreEqual(ConnectFourBoard.AiId, PlayGame(ConnectFourDifficulty.Hard), $"Hard lost or drew game {game + 1} vs random");
            }
        }

        // Plays random-mover (player, first) vs the AI at the given difficulty.
        // Returns the winning id, or 0 for a draw.
        private static int PlayGame(ConnectFourDifficulty difficulty)
        {
            var board = ConnectFourBoard.NewBoard();
            while (true)
            {
                List<int> valid = ConnectFourBoard.GetValidColumns(board);
                if (valid.Count == 0) return 0;
                int col = valid[Random.Range(0, valid.Count)];
                int row = ConnectFourBoard.DropPiece(board, col, ConnectFourBoard.PlayerId);
                if (ConnectFourBoard.CheckForWin(board, row, col, ConnectFourBoard.PlayerId)) return ConnectFourBoard.PlayerId;

                if (ConnectFourBoard.IsFull(board)) return 0;
                col = ConnectFourAI.ChooseColumn(board, difficulty);
                row = ConnectFourBoard.DropPiece(board, col, ConnectFourBoard.AiId);
                if (ConnectFourBoard.CheckForWin(board, row, col, ConnectFourBoard.AiId)) return ConnectFourBoard.AiId;
            }
        }
    }
}
