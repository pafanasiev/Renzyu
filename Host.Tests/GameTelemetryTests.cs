using System.Text.Json;
using Host.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Host.Tests
{
    [TestClass]
    public class GameTelemetryTests
    {
        [TestMethod]
        public void FileGameTelemetry_RecordedMovesArePreservedWhenComputerLoses()
        {
            string directory = CreateTemporaryDirectory();
            Guid gameId = Guid.NewGuid();

            try
            {
                var telemetry = new FileGameTelemetry(gameId, directory);
                telemetry.RecordMove(3, 4, ComputerGame.PLAYER_MARK, false);
                telemetry.RecordMove(5, 6, ComputerGame.COMPUTER_MARK, false);
                telemetry.RecordMove(7, 8, ComputerGame.PLAYER_MARK, true);

                string activePath = Path.Combine(directory, $"game-{gameId:N}.jsonl");
                byte[] contentsBeforeRename = File.ReadAllBytes(activePath);

                telemetry.MarkComputerLost();

                string lostPath = Path.Combine(directory, $"game-{gameId:N}.computer-lost.jsonl");
                CollectionAssert.AreEqual(contentsBeforeRename, File.ReadAllBytes(lostPath));
                Assert.IsFalse(File.Exists(activePath));

                string[] lines = File.ReadAllLines(lostPath);
                Assert.AreEqual(3, lines.Length);
                using JsonDocument firstMove = JsonDocument.Parse(lines[0]);
                using JsonDocument lastMove = JsonDocument.Parse(lines[2]);
                Assert.AreEqual(1, firstMove.RootElement.GetProperty("schemaVersion").GetInt32());
                Assert.AreEqual(gameId, firstMove.RootElement.GetProperty("gameId").GetGuid());
                Assert.AreEqual(1, firstMove.RootElement.GetProperty("turn").GetInt32());
                Assert.AreEqual("human", firstMove.RootElement.GetProperty("actor").GetString());
                Assert.AreEqual(7, lastMove.RootElement.GetProperty("x").GetInt32());
                Assert.AreEqual(8, lastMove.RootElement.GetProperty("y").GetInt32());
                Assert.IsTrue(lastMove.RootElement.GetProperty("won").GetBoolean());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void ComputerGame_RecordsAllMovesAndMarksHumanWin()
        {
            var telemetry = new RecordingGameTelemetry();
            var ai = new ScriptedAI(
                new Cell(0, 1),
                new Cell(1, 1),
                new Cell(2, 1),
                new Cell(3, 1));
            var game = new ComputerGame("player", ai, telemetry);

            try
            {
                for (int x = 0; x < GameBoard.WIN_LENGTH; x++)
                    game.MakeMove("player", x, 0);

                Assert.AreEqual(9, telemetry.Moves.Count);
                for (int turn = 0; turn < telemetry.Moves.Count; turn++)
                {
                    int expectedMark = turn % 2 == 0
                        ? ComputerGame.PLAYER_MARK
                        : ComputerGame.COMPUTER_MARK;
                    Assert.AreEqual(expectedMark, telemetry.Moves[turn].Mark);
                }

                RecordedMove winningMove = telemetry.Moves[telemetry.Moves.Count - 1];
                Assert.AreEqual(4, winningMove.X);
                Assert.AreEqual(0, winningMove.Y);
                Assert.IsTrue(winningMove.Won);
                Assert.IsTrue(telemetry.ComputerLost);
            }
            finally
            {
                game.End();
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"renzyu-telemetry-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private sealed class ScriptedAI : IAI
        {
            private readonly Queue<Cell> moves;

            public ScriptedAI(params Cell[] moves)
            {
                this.moves = new Queue<Cell>(moves);
            }

            public Cell GetBestMove(GameBoard board)
            {
                return moves.Dequeue();
            }

            public void EvaluateNodeRank(MinimaxNode current)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class RecordingGameTelemetry : IGameTelemetry
        {
            public List<RecordedMove> Moves { get; } = new List<RecordedMove>();
            public bool ComputerLost { get; private set; }

            public void RecordMove(int x, int y, int mark, bool won)
            {
                Moves.Add(new RecordedMove(x, y, mark, won));
            }

            public void MarkComputerLost()
            {
                ComputerLost = true;
            }
        }

        private sealed class RecordedMove
        {
            public RecordedMove(int x, int y, int mark, bool won)
            {
                X = x;
                Y = y;
                Mark = mark;
                Won = won;
            }

            public int X { get; }
            public int Y { get; }
            public int Mark { get; }
            public bool Won { get; }
        }
    }
}
