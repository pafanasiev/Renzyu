using Host.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Host.Tests
{
    [TestClass]
    public class ReinforcementAITests
    {
        [TestMethod]
        public void ReinforcementAI_ImmediateWin_SelectsWinningMove()
        {
            var board = new int[GameBoard.SIZE, GameBoard.SIZE];
            board[4, 9] = ComputerGame.PLAYER_MARK;
            board[5, 9] = ComputerGame.COMPUTER_MARK;
            board[6, 9] = ComputerGame.COMPUTER_MARK;
            board[7, 9] = ComputerGame.COMPUTER_MARK;
            board[8, 9] = ComputerGame.COMPUTER_MARK;
            var model = CreateModel("winner");
            model.Weights[FeatureIndex("immediate_win")] = 10;

            Cell move = new ReinforcementAI(model).GetBestMove(new GameBoard(board));

            Assert.AreEqual(9, move.X);
            Assert.AreEqual(9, move.Y);
        }

        [TestMethod]
        public void ReinforcementAI_ImmediateThreat_BlocksOpponent()
        {
            var board = new int[GameBoard.SIZE, GameBoard.SIZE];
            board[4, 9] = ComputerGame.COMPUTER_MARK;
            board[5, 9] = ComputerGame.PLAYER_MARK;
            board[6, 9] = ComputerGame.PLAYER_MARK;
            board[7, 9] = ComputerGame.PLAYER_MARK;
            board[8, 9] = ComputerGame.PLAYER_MARK;
            var model = CreateModel("blocker");
            model.Weights[FeatureIndex("immediate_block")] = 10;

            Cell move = new ReinforcementAI(model).GetBestMove(new GameBoard(board));

            Assert.AreEqual(9, move.X);
            Assert.AreEqual(9, move.Y);
        }

        [TestMethod]
        public void ReinforcementAI_DistilledPolicyPosition_TakesRewardedMove()
        {
            var board = new GameBoard();
            board.Move(9, 9, ComputerGame.PLAYER_MARK);
            var model = CreateModel("distilled");
            model.PolicyEntries.Add(new ReinforcementPolicyEntry
            {
                StateKey = LearnedPolicy.GetStateKey(board, ComputerGame.COMPUTER_MARK),
                X = 0,
                Y = 0,
                Value = 0.8,
                Visits = 1,
            });

            Cell move = new ReinforcementAI(model).GetBestMove(board);

            Assert.AreEqual(0, move.X);
            Assert.AreEqual(0, move.Y);
        }

        [TestMethod]
        public void ReinforcementAI_IllegalDistilledMove_UsesLegalFallback()
        {
            var board = new GameBoard();
            board.Move(9, 9, ComputerGame.PLAYER_MARK);
            var model = CreateModel("invalid-distilled-move");
            model.PolicyEntries.Add(new ReinforcementPolicyEntry
            {
                StateKey = LearnedPolicy.GetStateKey(board, ComputerGame.COMPUTER_MARK),
                X = 9,
                Y = 9,
                Value = 1,
                Visits = 1,
            });

            Cell move = new ReinforcementAI(model).GetBestMove(board);

            Assert.AreEqual(0, board.Value(move.X, move.Y));
        }

        [TestMethod]
        public void ComputerPlayerQueue_UsesRequestingConnectionsModel()
        {
            string telemetryDirectory = CreateTemporaryDirectory();
            string previousTelemetryDirectory = Environment.GetEnvironmentVariable(
                FileGameTelemetry.DirectoryEnvironmentVariable);
            var catalog = new RecordingCatalog();
            Game game = null;

            try
            {
                Environment.SetEnvironmentVariable(
                    FileGameTelemetry.DirectoryEnvironmentVariable,
                    telemetryDirectory);
                lock (PlayerQueue.lockObject)
                    PlayerQueue.GameRequests.Clear();

                PlayerQueue.Add(
                    new GameRequest
                    {
                        Connection = "first-player",
                        IsComputerGame = true,
                        AiModelId = "first-model",
                    },
                    catalog);
                PlayerQueue secondQueue = PlayerQueue.Add(
                    new GameRequest
                    {
                        Connection = "second-player",
                        IsComputerGame = true,
                        AiModelId = "second-model",
                    },
                    catalog);

                game = secondQueue.GetGame();

                Assert.AreEqual("second-model", catalog.CreatedModelId);
                Assert.IsTrue(game.GetPlayersWithMarks().ContainsKey("second-player"));
                Assert.IsTrue(PlayerQueue.GameRequests.Any(
                    request => request.Connection == "first-player"));
            }
            finally
            {
                game?.End();
                PlayerQueue.Dequeue("first-player", "second-player");
                Environment.SetEnvironmentVariable(
                    FileGameTelemetry.DirectoryEnvironmentVariable,
                    previousTelemetryDirectory);
                Directory.Delete(telemetryDirectory, recursive: true);
            }
        }

        [TestMethod]
        public void ReinforcementModelStore_SaveAndLoad_PreservesModel()
        {
            string directory = CreateTemporaryDirectory();
            string path = Path.Combine(directory, "round-trip.json");
            var expected = CreateModel("round-trip");
            expected.Weights[FeatureIndex("own_fork")] = 1.25;

            try
            {
                ReinforcementModelStore.Save(expected, path);
                ReinforcementModel actual = ReinforcementModelStore.Load(path);

                Assert.AreEqual(expected.Id, actual.Id);
                Assert.AreEqual(expected.Name, actual.Name);
                Assert.AreEqual(expected.EpisodesTrained, actual.EpisodesTrained);
                CollectionAssert.AreEqual(expected.FeatureNames, actual.FeatureNames);
                CollectionAssert.AreEqual(expected.Weights, actual.Weights);
                Assert.AreEqual(expected.PolicyEntries.Count, actual.PolicyEntries.Count);
                Assert.AreEqual(expected.Evaluation.Score, actual.Evaluation.Score);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void FileAiModelCatalog_SavedModels_AreDiscoverableAndPlayable()
        {
            string directory = CreateTemporaryDirectory();
            var model = CreateModel("catalog-agent");
            model.Name = "Catalog agent";
            model.Weights[FeatureIndex("immediate_win")] = 10;

            try
            {
                ReinforcementModelStore.Save(model, Path.Combine(directory, "catalog-agent.json"));
                var catalog = new FileAiModelCatalog(
                    directory,
                    NullLogger<FileAiModelCatalog>.Instance);

                IReadOnlyList<AiModelDescriptor> available = catalog.GetAvailableModels();
                IAI ai = catalog.CreateAI(model.Id);

                Assert.AreEqual(2, available.Count);
                Assert.AreEqual(FileAiModelCatalog.MinimaxModelId, available[0].Id);
                Assert.AreEqual(model.Id, available[1].Id);
                Assert.AreEqual(model.Name, available[1].Name);
                Assert.IsInstanceOfType<ReinforcementAI>(ai);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void FileAiModelCatalog_InvalidModel_IsNotOffered()
        {
            string directory = CreateTemporaryDirectory();
            File.WriteAllText(Path.Combine(directory, "invalid.json"), "{not-json");

            try
            {
                var catalog = new FileAiModelCatalog(
                    directory,
                    NullLogger<FileAiModelCatalog>.Instance);

                IReadOnlyList<AiModelDescriptor> available = catalog.GetAvailableModels();

                Assert.AreEqual(1, available.Count);
                Assert.AreEqual(FileAiModelCatalog.MinimaxModelId, available[0].Id);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void FileAiModelCatalog_UnknownModel_Throws()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                var catalog = new FileAiModelCatalog(
                    directory,
                    NullLogger<FileAiModelCatalog>.Instance);

                try
                {
                    catalog.CreateAI("missing-model");
                    Assert.Fail("Expected an unknown model id to be rejected.");
                }
                catch (KeyNotFoundException)
                {
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static ReinforcementModel CreateModel(string id)
        {
            return new ReinforcementModel
            {
                Id = id,
                Name = id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                EpisodesTrained = 50,
                FeatureNames = LearnedPolicy.FeatureNames.ToArray(),
                Weights = new double[LearnedPolicy.FeatureNames.Count],
                Training = new ReinforcementTrainingMetadata
                {
                    OpponentDepth = 2,
                    OpponentMaxNodes = 12000,
                    TeacherDepth = 6,
                    TeacherMaxNodes = 120000,
                    LearningRate = 0.04,
                    DiscountFactor = 0.90,
                    InitialEpsilon = 0.35,
                    FinalEpsilon = 0.03,
                    TimeLimitSeconds = 300,
                    TargetScore = 0.75,
                    TargetReached = true,
                    Seed = 1337,
                },
                Evaluation = new ReinforcementEvaluation
                {
                    Games = 2,
                    Wins = 1,
                    Draws = 1,
                    Losses = 0,
                },
            };
        }

        private static int FeatureIndex(string name)
        {
            return LearnedPolicy.FeatureNames
                .Select((feature, index) => new { feature, index })
                .Single(item => item.feature == name)
                .index;
        }

        private static string CreateTemporaryDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "renzyu-models-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private sealed class RecordingCatalog : IAiModelCatalog
        {
            public string CreatedModelId { get; private set; }

            public IReadOnlyList<AiModelDescriptor> GetAvailableModels()
            {
                return Array.Empty<AiModelDescriptor>();
            }

            public IAI CreateAI(string modelId)
            {
                CreatedModelId = modelId;
                return new AI(1, 100);
            }
        }
    }
}
