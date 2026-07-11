using System.Diagnostics;
using Host.Models;

namespace Renzyu.Training
{
    internal enum MatchOutcome
    {
        Win,
        Draw,
        Loss,
    }

    internal sealed class TrainingSnapshot
    {
        public int Episode { get; init; }
        public int TotalEpisodes { get; init; }
        public double Epsilon { get; init; }
        public MatchOutcome LastOutcome { get; init; }
        public int Wins { get; init; }
        public int Draws { get; init; }
        public int Losses { get; init; }
        public double LastReward { get; init; }
        public double MeanAbsoluteTdError { get; init; }
        public int Moves { get; init; }
        public int[,] Board { get; init; }
        public Cell LastMove { get; init; }
        public ReinforcementEvaluation Evaluation { get; init; }
        public IReadOnlyList<double> EvaluationHistory { get; init; }
        public IReadOnlyList<double> Weights { get; init; }
        public TimeSpan Elapsed { get; init; }
        public string CheckpointPath { get; init; }
        public int PolicyPositions { get; init; }
        public double TargetScore { get; init; }
        public bool TargetReached { get; init; }
        public bool IsComplete { get; init; }
        public bool IsCancelled { get; init; }
    }

    internal sealed class TrainingResult
    {
        public string ModelPath { get; init; }
        public TrainingSnapshot Snapshot { get; init; }
        public bool TargetReached { get; init; }
    }

    internal sealed class ReinforcementTrainer
    {
        private const double DrawReward = 0.25;
        private const double MaximumWeight = 10;

        private readonly TrainingOptions options;
        private readonly Random random;
        private readonly AI minimax;
        private readonly AI teacher;
        private readonly double[] weights;
        private readonly Dictionary<string, ReinforcementPolicyEntry> policyEntries =
            new Dictionary<string, ReinforcementPolicyEntry>(StringComparer.Ordinal);
        private readonly string modelId;
        private readonly DateTimeOffset startedAtUtc;
        private readonly List<double> evaluationHistory = new List<double>();
        private ReinforcementEvaluation latestEvaluation = new ReinforcementEvaluation();
        private double[] bestWeights;
        private List<ReinforcementPolicyEntry> bestPolicyEntries;
        private ReinforcementEvaluation bestEvaluation;
        private int bestEpisode;

        public ReinforcementTrainer(TrainingOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            random = new Random(options.Seed);
            minimax = new AI(options.OpponentDepth, options.OpponentMaxNodes);
            teacher = new AI(options.TeacherDepth, options.TeacherMaxNodes);
            weights = LearnedPolicy.CreateInitialWeights();
            startedAtUtc = DateTimeOffset.UtcNow;
            modelId = CreateModelId(options.Name, startedAtUtc);
        }

        public TrainingResult Train(CancellationToken cancellationToken, Action<TrainingSnapshot> reportProgress)
        {
            ArgumentNullException.ThrowIfNull(reportProgress);
            Directory.CreateDirectory(options.OutputDirectory);

            var stopwatch = Stopwatch.StartNew();
            int wins = 0;
            int draws = 0;
            int losses = 0;
            int completedEpisodes = 0;
            EpisodeResult lastEpisode = null;
            string checkpointPath = null;
            bool targetReached = false;

            for (int learnerMark = ComputerGame.PLAYER_MARK;
                learnerMark <= ComputerGame.COMPUTER_MARK
                    && completedEpisodes < options.Episodes
                    && !ShouldStop(cancellationToken, stopwatch);
                learnerMark++)
            {
                lastEpisode = PlayExpertEpisode(learnerMark);
                completedEpisodes++;
                AddOutcome(lastEpisode.Outcome, ref wins, ref draws, ref losses);
            }

            if (completedEpisodes == options.Episodes
                && !ShouldStop(cancellationToken, stopwatch))
            {
                latestEvaluation = Evaluate(cancellationToken);
                evaluationHistory.Add(latestEvaluation.Score);
                CaptureBest(completedEpisodes);
                targetReached = HasReachedTarget(latestEvaluation);
                reportProgress(CreateSnapshot(
                    completedEpisodes,
                    GetEpsilon(completedEpisodes),
                    lastEpisode,
                    wins,
                    draws,
                    losses,
                    stopwatch.Elapsed,
                    null,
                    false,
                    false,
                    targetReached));
            }

            for (int episode = completedEpisodes + 1;
                episode <= options.Episodes
                    && !targetReached
                    && !ShouldStop(cancellationToken, stopwatch);
                episode++)
            {
                double epsilon = GetEpsilon(episode);
                lastEpisode = PlayTrainingEpisode(epsilon);
                completedEpisodes = episode;
                AddOutcome(lastEpisode.Outcome, ref wins, ref draws, ref losses);

                if (episode % options.EvaluateEvery == 0 || episode == options.Episodes)
                {
                    latestEvaluation = Evaluate(cancellationToken);
                    evaluationHistory.Add(latestEvaluation.Score);
                    CaptureBest(episode);
                    targetReached = HasReachedTarget(latestEvaluation);
                }

                checkpointPath = null;
                if (options.CheckpointEvery > 0
                    && episode % options.CheckpointEvery == 0
                    && episode < options.Episodes)
                {
                    string checkpointId = modelId + "-ep" + episode.ToString("D6");
                    checkpointPath = Path.Combine(options.OutputDirectory, checkpointId + ".json");
                    ReinforcementModelStore.Save(
                        CreateModel(
                            checkpointId,
                            options.Name + " (episode " + episode + ")",
                            episode,
                            latestEvaluation,
                            targetReached),
                        checkpointPath);
                }

                reportProgress(CreateSnapshot(
                    completedEpisodes,
                    epsilon,
                    lastEpisode,
                    wins,
                    draws,
                    losses,
                    stopwatch.Elapsed,
                    checkpointPath,
                    false,
                    false,
                    targetReached));
            }

            RestoreBestPolicy();
            targetReached = HasReachedTarget(bestEvaluation);
            bool cancelled = cancellationToken.IsCancellationRequested
                || (!targetReached
                    && completedEpisodes < options.Episodes
                    && stopwatch.Elapsed.TotalSeconds >= options.TimeLimitSeconds);
            string modelPath = Path.Combine(options.OutputDirectory, modelId + ".json");
            ReinforcementModelStore.Save(
                CreateModel(
                    modelId,
                    options.Name,
                    bestEpisode == 0 ? completedEpisodes : bestEpisode,
                    latestEvaluation,
                    targetReached),
                modelPath);

            var finalSnapshot = CreateSnapshot(
                completedEpisodes,
                completedEpisodes == 0 ? options.InitialEpsilon : GetEpsilon(completedEpisodes),
                lastEpisode,
                wins,
                draws,
                losses,
                stopwatch.Elapsed,
                modelPath,
                true,
                cancelled,
                targetReached);
            reportProgress(finalSnapshot);

            return new TrainingResult
            {
                ModelPath = modelPath,
                Snapshot = finalSnapshot,
                TargetReached = targetReached,
            };
        }

        private EpisodeResult PlayExpertEpisode(int learnerMark)
        {
            int opponentMark = Invert(learnerMark);
            int currentMark = ComputerGame.PLAYER_MARK;
            int moves = 0;
            var board = new GameBoard();
            var trajectory = new List<TrajectoryStep>();
            Cell lastMove = null;

            while (true)
            {
                Cell move;
                if (currentMark == learnerMark)
                {
                    move = GetMinimaxMove(teacher, board, learnerMark);
                    trajectory.Add(new TrajectoryStep
                    {
                        StateKey = LearnedPolicy.GetStateKey(board, learnerMark),
                        Move = move,
                    });
                }
                else
                {
                    move = GetMinimaxMove(minimax, board, opponentMark);
                }

                Row win = board.Move(move.X, move.Y, currentMark);
                moves++;
                lastMove = move;
                if (win != null || IsFull(board))
                {
                    MatchOutcome outcome = win == null
                        ? MatchOutcome.Draw
                        : currentMark == learnerMark
                            ? MatchOutcome.Win
                            : MatchOutcome.Loss;
                    double reward = outcome == MatchOutcome.Win
                        ? 1
                        : outcome == MatchOutcome.Draw
                            ? DrawReward
                            : -1;
                    if (reward > 0)
                        ApplyTrajectory(trajectory, reward);

                    return new EpisodeResult
                    {
                        Outcome = outcome,
                        Reward = reward,
                        Moves = moves,
                        Board = CopyBoard(board),
                        LastMove = lastMove,
                    };
                }

                currentMark = Invert(currentMark);
            }
        }

        private EpisodeResult PlayTrainingEpisode(double epsilon)
        {
            var board = new GameBoard();
            var opponentOpening = GetMinimaxMove(
                minimax,
                board,
                ComputerGame.PLAYER_MARK);
            board.Move(opponentOpening.X, opponentOpening.Y, ComputerGame.PLAYER_MARK);

            int moves = 1;
            double rewardTotal = 0;
            double absoluteErrorTotal = 0;
            int updates = 0;
            Cell lastMove = opponentOpening;

            while (true)
            {
                var decision = SelectTrainingMove(board, epsilon);
                double shapedReward = LearnedPolicy.GetShapedReward(decision.Features);
                Row learnerWin = board.Move(
                    decision.Move.X,
                    decision.Move.Y,
                    ComputerGame.COMPUTER_MARK);
                moves++;
                lastMove = decision.Move;

                double target;
                MatchOutcome? terminalOutcome = null;
                if (learnerWin != null)
                {
                    target = 1 + shapedReward;
                    terminalOutcome = MatchOutcome.Win;
                }
                else if (IsFull(board))
                {
                    target = DrawReward + shapedReward;
                    terminalOutcome = MatchOutcome.Draw;
                }
                else
                {
                    var opponentMove = GetMinimaxMove(
                        minimax,
                        board,
                        ComputerGame.PLAYER_MARK);
                    Row opponentWin = board.Move(
                        opponentMove.X,
                        opponentMove.Y,
                        ComputerGame.PLAYER_MARK);
                    moves++;
                    lastMove = opponentMove;

                    if (opponentWin != null)
                    {
                        target = -1 + shapedReward;
                        terminalOutcome = MatchOutcome.Loss;
                    }
                    else if (IsFull(board))
                    {
                        target = DrawReward + shapedReward;
                        terminalOutcome = MatchOutcome.Draw;
                    }
                    else
                    {
                        target = shapedReward
                            + (options.DiscountFactor * LearnedPolicy.GetMaxScore(
                                board,
                                ComputerGame.COMPUTER_MARK,
                                weights));
                    }
                }

                double tdError = UpdateWeights(decision, target, updates);
                rewardTotal += terminalOutcome.HasValue
                    ? target
                    : shapedReward;
                absoluteErrorTotal += Math.Abs(tdError);
                updates++;

                if (terminalOutcome.HasValue)
                {
                    return new EpisodeResult
                    {
                        Outcome = terminalOutcome.Value,
                        Reward = rewardTotal,
                        MeanAbsoluteTdError = absoluteErrorTotal / updates,
                        Moves = moves,
                        Board = CopyBoard(board),
                        LastMove = lastMove,
                    };
                }
            }
        }

        private ReinforcementEvaluation Evaluate(CancellationToken cancellationToken)
        {
            var result = new ReinforcementEvaluation();
            for (int game = 0; game < options.EvaluationGames; game++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                int learnerMark = game % 2 == 0
                    ? ComputerGame.COMPUTER_MARK
                    : ComputerGame.PLAYER_MARK;
                MatchOutcome outcome = PlayEvaluationGame(learnerMark);
                result.Games++;
                switch (outcome)
                {
                    case MatchOutcome.Win:
                        result.Wins++;
                        break;
                    case MatchOutcome.Draw:
                        result.Draws++;
                        break;
                    case MatchOutcome.Loss:
                        result.Losses++;
                        break;
                }
            }
            return result;
        }

        private MatchOutcome PlayEvaluationGame(int learnerMark)
        {
            int opponentMark = Invert(learnerMark);
            int currentMark = ComputerGame.PLAYER_MARK;
            var board = new GameBoard();

            while (true)
            {
                Cell move = currentMark == learnerMark
                    ? SelectPolicyMove(board, learnerMark)
                    : GetMinimaxMove(minimax, board, opponentMark);
                Row win = board.Move(move.X, move.Y, currentMark);
                if (win != null)
                    return currentMark == learnerMark ? MatchOutcome.Win : MatchOutcome.Loss;
                if (IsFull(board))
                    return MatchOutcome.Draw;
                currentMark = Invert(currentMark);
            }
        }

        private PolicyDecision SelectTrainingMove(GameBoard board, double epsilon)
        {
            bool explore = random.NextDouble() < epsilon;
            string stateKey = LearnedPolicy.GetStateKey(board, ComputerGame.COMPUTER_MARK);
            if (!explore && policyEntries.TryGetValue(stateKey, out var entry))
            {
                var move = new Cell(entry.X, entry.Y);
                var features = LearnedPolicy.ExtractFeatures(
                    board,
                    move,
                    ComputerGame.COMPUTER_MARK);
                return new PolicyDecision
                {
                    Move = move,
                    Features = features,
                    Score = LearnedPolicy.Score(features, weights),
                };
            }

            return LearnedPolicy.SelectMove(
                board,
                ComputerGame.COMPUTER_MARK,
                weights,
                random,
                explore ? 1 : 0);
        }

        private Cell SelectPolicyMove(GameBoard board, int learnerMark)
        {
            string stateKey = LearnedPolicy.GetStateKey(board, learnerMark);
            if (policyEntries.TryGetValue(stateKey, out var entry))
            {
                if (board.Value(entry.X, entry.Y) != 0)
                    throw new InvalidDataException("The trained policy selected an occupied cell.");
                return new Cell(entry.X, entry.Y);
            }

            return LearnedPolicy.SelectMove(board, learnerMark, weights).Move;
        }

        private void ApplyTrajectory(IReadOnlyList<TrajectoryStep> trajectory, double terminalReward)
        {
            double value = terminalReward;
            for (int index = trajectory.Count - 1; index >= 0; index--)
            {
                TrajectoryStep step = trajectory[index];
                if (policyEntries.TryGetValue(step.StateKey, out var existing))
                {
                    if (existing.X == step.Move.X && existing.Y == step.Move.Y)
                    {
                        existing.Value = ((existing.Value * existing.Visits) + value)
                            / (existing.Visits + 1);
                        existing.Visits++;
                    }
                    else if (value > existing.Value)
                    {
                        policyEntries[step.StateKey] = CreatePolicyEntry(step, value);
                    }
                }
                else
                {
                    policyEntries.Add(step.StateKey, CreatePolicyEntry(step, value));
                }
                value *= options.DiscountFactor;
            }
        }

        private static ReinforcementPolicyEntry CreatePolicyEntry(
            TrajectoryStep step,
            double value)
        {
            return new ReinforcementPolicyEntry
            {
                StateKey = step.StateKey,
                X = step.Move.X,
                Y = step.Move.Y,
                Value = value,
                Visits = 1,
            };
        }

        private static Cell GetMinimaxMove(AI ai, GameBoard board, int mark)
        {
            ArgumentNullException.ThrowIfNull(ai);
            if (mark == ComputerGame.COMPUTER_MARK)
                return ai.GetBestMove(board);

            var mirrored = new int[board.Width, board.Height];
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    int value = board.Value(x, y);
                    mirrored[x, y] = value == ComputerGame.PLAYER_MARK
                        ? ComputerGame.COMPUTER_MARK
                        : value == ComputerGame.COMPUTER_MARK
                            ? ComputerGame.PLAYER_MARK
                            : 0;
                }
            }
            return ai.GetBestMove(new GameBoard(mirrored));
        }

        private double UpdateWeights(PolicyDecision decision, double target, int updateNumber)
        {
            double error = target - decision.Score;
            double boundedError = Math.Clamp(error, -2, 2);
            double featureNorm = Math.Max(1, decision.Features.Sum(value => value * value));
            double decayedLearningRate = options.LearningRate / Math.Sqrt(1 + (updateNumber * 0.001));
            double step = decayedLearningRate * boundedError / featureNorm;

            for (int index = 0; index < weights.Length; index++)
            {
                weights[index] = Math.Clamp(
                    weights[index] + (step * decision.Features[index]),
                    -MaximumWeight,
                    MaximumWeight);
            }
            return error;
        }

        private ReinforcementModel CreateModel(
            string id,
            string name,
            int episodes,
            ReinforcementEvaluation evaluation,
            bool targetReached)
        {
            return new ReinforcementModel
            {
                Id = id,
                Name = name,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                EpisodesTrained = episodes,
                FeatureNames = LearnedPolicy.FeatureNames.ToArray(),
                Weights = (double[])weights.Clone(),
                PolicyEntries = ClonePolicyEntries(),
                Training = new ReinforcementTrainingMetadata
                {
                    Opponent = "minimax",
                    OpponentDepth = options.OpponentDepth,
                    OpponentMaxNodes = options.OpponentMaxNodes,
                    TeacherDepth = options.TeacherDepth,
                    TeacherMaxNodes = options.TeacherMaxNodes,
                    LearningRate = options.LearningRate,
                    DiscountFactor = options.DiscountFactor,
                    InitialEpsilon = options.InitialEpsilon,
                    FinalEpsilon = options.FinalEpsilon,
                    TimeLimitSeconds = options.TimeLimitSeconds,
                    TargetScore = options.TargetScore,
                    TargetReached = targetReached,
                    Seed = options.Seed,
                },
                Evaluation = new ReinforcementEvaluation
                {
                    Games = evaluation.Games,
                    Wins = evaluation.Wins,
                    Draws = evaluation.Draws,
                    Losses = evaluation.Losses,
                },
            };
        }

        private TrainingSnapshot CreateSnapshot(
            int episode,
            double epsilon,
            EpisodeResult lastEpisode,
            int wins,
            int draws,
            int losses,
            TimeSpan elapsed,
            string checkpointPath,
            bool isComplete,
            bool isCancelled,
            bool targetReached)
        {
            return new TrainingSnapshot
            {
                Episode = episode,
                TotalEpisodes = options.Episodes,
                Epsilon = epsilon,
                LastOutcome = lastEpisode == null ? MatchOutcome.Draw : lastEpisode.Outcome,
                Wins = wins,
                Draws = draws,
                Losses = losses,
                LastReward = lastEpisode == null ? 0 : lastEpisode.Reward,
                MeanAbsoluteTdError = lastEpisode == null ? 0 : lastEpisode.MeanAbsoluteTdError,
                Moves = lastEpisode == null ? 0 : lastEpisode.Moves,
                Board = lastEpisode == null ? new int[GameBoard.SIZE, GameBoard.SIZE] : lastEpisode.Board,
                LastMove = lastEpisode?.LastMove,
                Evaluation = latestEvaluation,
                EvaluationHistory = evaluationHistory.ToArray(),
                Weights = (double[])weights.Clone(),
                Elapsed = elapsed,
                CheckpointPath = checkpointPath,
                PolicyPositions = policyEntries.Count,
                TargetScore = options.TargetScore,
                TargetReached = targetReached,
                IsComplete = isComplete,
                IsCancelled = isCancelled,
            };
        }

        private void CaptureBest(int episode)
        {
            if (latestEvaluation.Games != options.EvaluationGames
                || bestEvaluation != null
                    && latestEvaluation.Score <= bestEvaluation.Score)
            {
                return;
            }

            bestWeights = (double[])weights.Clone();
            bestPolicyEntries = ClonePolicyEntries();
            bestEvaluation = CloneEvaluation(latestEvaluation);
            bestEpisode = episode;
        }

        private void RestoreBestPolicy()
        {
            if (bestEvaluation == null)
                return;

            Array.Copy(bestWeights, weights, weights.Length);
            policyEntries.Clear();
            foreach (var entry in bestPolicyEntries)
                policyEntries.Add(entry.StateKey, ClonePolicyEntry(entry));
            latestEvaluation = CloneEvaluation(bestEvaluation);
        }

        private List<ReinforcementPolicyEntry> ClonePolicyEntries()
        {
            return policyEntries.Values
                .OrderBy(entry => entry.StateKey, StringComparer.Ordinal)
                .Select(ClonePolicyEntry)
                .ToList();
        }

        private static ReinforcementPolicyEntry ClonePolicyEntry(ReinforcementPolicyEntry entry)
        {
            return new ReinforcementPolicyEntry
            {
                StateKey = entry.StateKey,
                X = entry.X,
                Y = entry.Y,
                Value = entry.Value,
                Visits = entry.Visits,
            };
        }

        private static ReinforcementEvaluation CloneEvaluation(ReinforcementEvaluation evaluation)
        {
            return new ReinforcementEvaluation
            {
                Games = evaluation.Games,
                Wins = evaluation.Wins,
                Draws = evaluation.Draws,
                Losses = evaluation.Losses,
            };
        }

        private bool ShouldStop(CancellationToken cancellationToken, Stopwatch stopwatch)
        {
            return cancellationToken.IsCancellationRequested
                || stopwatch.Elapsed.TotalSeconds >= options.TimeLimitSeconds;
        }

        private bool HasReachedTarget(ReinforcementEvaluation evaluation)
        {
            return evaluation != null
                && evaluation.Games == options.EvaluationGames
                && evaluation.Score >= options.TargetScore;
        }

        private static void AddOutcome(
            MatchOutcome outcome,
            ref int wins,
            ref int draws,
            ref int losses)
        {
            switch (outcome)
            {
                case MatchOutcome.Win:
                    wins++;
                    break;
                case MatchOutcome.Draw:
                    draws++;
                    break;
                case MatchOutcome.Loss:
                    losses++;
                    break;
            }
        }

        private double GetEpsilon(int episode)
        {
            if (options.Episodes == 1)
                return options.FinalEpsilon;
            double progress = (episode - 1.0) / (options.Episodes - 1.0);
            return options.InitialEpsilon
                + ((options.FinalEpsilon - options.InitialEpsilon) * progress);
        }

        private static bool IsFull(GameBoard board)
        {
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Value(x, y) == 0)
                        return false;
                }
            }
            return true;
        }

        private static int[,] CopyBoard(GameBoard board)
        {
            var result = new int[board.Width, board.Height];
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                    result[x, y] = board.Value(x, y);
            }
            return result;
        }

        private static int Invert(int mark)
        {
            return mark == ComputerGame.COMPUTER_MARK
                ? ComputerGame.PLAYER_MARK
                : ComputerGame.COMPUTER_MARK;
        }

        private static string CreateModelId(string name, DateTimeOffset timestamp)
        {
            string slug = ReinforcementModelStore.Slugify(name);
            if (slug.Length > 92)
                slug = slug.Substring(0, 92).TrimEnd('-');
            return slug
                + "-"
                + timestamp.ToString("yyyyMMdd-HHmmssfff");
        }

        private sealed class EpisodeResult
        {
            public MatchOutcome Outcome { get; init; }
            public double Reward { get; init; }
            public double MeanAbsoluteTdError { get; init; }
            public int Moves { get; init; }
            public int[,] Board { get; init; }
            public Cell LastMove { get; init; }
        }

        private sealed class TrajectoryStep
        {
            public string StateKey { get; init; }
            public Cell Move { get; init; }
        }
    }
}
