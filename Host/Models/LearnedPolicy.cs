using System.Security.Cryptography;

namespace Host.Models
{
    public sealed class PolicyDecision
    {
        public Cell Move { get; init; }
        public double[] Features { get; init; }
        public double Score { get; init; }
    }

    public static class LearnedPolicy
    {
        private const int CandidateRadius = 2;
        private const double ScoreTolerance = 0.000000001;
        private static readonly int[] DirectionX = { 1, 0, 1, 1 };
        private static readonly int[] DirectionY = { 0, 1, 1, -1 };

        private const int Bias = 0;
        private const int CenterControl = 1;
        private const int OwnAdjacent = 2;
        private const int OpponentAdjacent = 3;
        private const int OwnTwo = 4;
        private const int OwnThree = 5;
        private const int OwnFour = 6;
        private const int OpponentTwo = 7;
        private const int OpponentThree = 8;
        private const int OpponentFour = 9;
        private const int OwnOpenEnds = 10;
        private const int OpponentOpenEnds = 11;
        private const int ImmediateWin = 12;
        private const int ImmediateBlock = 13;
        private const int OwnFork = 14;
        private const int BlockFork = 15;

        public static IReadOnlyList<string> FeatureNames { get; } = Array.AsReadOnly(new[]
        {
            "bias",
            "center_control",
            "own_adjacent",
            "opponent_adjacent",
            "own_two_plus",
            "own_three_plus",
            "own_four_plus",
            "opponent_two_plus",
            "opponent_three_plus",
            "opponent_four_plus",
            "own_open_ends",
            "opponent_open_ends",
            "immediate_win",
            "immediate_block",
            "own_fork",
            "block_fork",
        });

        public static double[] CreateInitialWeights()
        {
            return new[]
            {
                0.0,
                0.08,
                0.08,
                0.06,
                0.08,
                0.22,
                0.75,
                0.08,
                0.28,
                0.70,
                0.04,
                0.05,
                4.0,
                3.5,
                1.0,
                1.0,
            };
        }

        public static PolicyDecision SelectMove(
            GameBoard board,
            int mark,
            IReadOnlyList<double> weights,
            Random random = null,
            double epsilon = 0)
        {
            ValidateInputs(board, mark, weights);
            if (epsilon < 0 || epsilon > 1)
                throw new ArgumentOutOfRangeException("epsilon");

            var candidates = GetCandidateMoves(board);
            if (candidates.Count == 0)
                throw new InvalidOperationException("No legal moves are available.");

            if (random != null && random.NextDouble() < epsilon)
            {
                var exploratoryMove = candidates[random.Next(candidates.Count)];
                var exploratoryFeatures = ExtractFeatures(board, exploratoryMove, mark);
                return new PolicyDecision
                {
                    Move = exploratoryMove,
                    Features = exploratoryFeatures,
                    Score = Score(exploratoryFeatures, weights),
                };
            }

            PolicyDecision best = null;
            int tiedMoves = 0;
            foreach (var candidate in candidates)
            {
                var features = ExtractFeatures(board, candidate, mark);
                var score = Score(features, weights);
                if (best == null || score > best.Score + ScoreTolerance)
                {
                    best = new PolicyDecision { Move = candidate, Features = features, Score = score };
                    tiedMoves = 1;
                }
                else if (random != null && Math.Abs(score - best.Score) <= ScoreTolerance)
                {
                    tiedMoves++;
                    if (random.Next(tiedMoves) == 0)
                        best = new PolicyDecision { Move = candidate, Features = features, Score = score };
                }
            }

            return best;
        }

        public static double GetMaxScore(GameBoard board, int mark, IReadOnlyList<double> weights)
        {
            return SelectMove(board, mark, weights).Score;
        }

        public static string GetStateKey(GameBoard board, int mark)
        {
            ArgumentNullException.ThrowIfNull(board);
            ValidateMark(mark);

            var state = new byte[3 + (board.Width * board.Height)];
            state[0] = checked((byte)board.Width);
            state[1] = checked((byte)board.Height);
            state[2] = checked((byte)mark);
            int index = 3;
            int opponentMark = Invert(mark);
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    int value = board.Value(x, y);
                    state[index++] = value == mark
                        ? (byte)1
                        : value == opponentMark
                            ? (byte)2
                            : (byte)0;
                }
            }
            return Convert.ToHexString(SHA256.HashData(state));
        }

        public static IReadOnlyList<Cell> GetCandidateMoves(GameBoard board)
        {
            ArgumentNullException.ThrowIfNull(board);
            var result = new List<Cell>();
            var hasOccupiedCell = false;

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Value(x, y) != 0)
                    {
                        hasOccupiedCell = true;
                        break;
                    }
                }
                if (hasOccupiedCell)
                    break;
            }

            if (!hasOccupiedCell)
            {
                if (board.Width == 0 || board.Height == 0)
                    return result;
                result.Add(new Cell(board.Width / 2, board.Height / 2));
                return result;
            }

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Value(x, y) == 0 && IsNearOccupiedCell(board, x, y))
                        result.Add(new Cell(x, y));
                }
            }

            if (result.Count > 0)
                return result;

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Value(x, y) == 0)
                        result.Add(new Cell(x, y));
                }
            }
            return result;
        }

        public static double[] ExtractFeatures(GameBoard board, Cell move, int mark)
        {
            ArgumentNullException.ThrowIfNull(board);
            ArgumentNullException.ThrowIfNull(move);
            ValidateMark(mark);
            if (!board.IsOnBoard(move) || board.Value(move.X, move.Y) != 0)
                throw new ArgumentException("The policy move must be an empty cell on the board.", "move");

            var features = new double[FeatureNames.Count];
            int opponentMark = Invert(mark);
            features[Bias] = 1;

            double centerX = (board.Width - 1) / 2.0;
            double centerY = (board.Height - 1) / 2.0;
            double maximumCenterDistance = Math.Max(1, centerX + centerY);
            double centerDistance = Math.Abs(move.X - centerX) + Math.Abs(move.Y - centerY);
            features[CenterControl] = 1 - (centerDistance / maximumCenterDistance);

            for (int nearbyX = Math.Max(0, move.X - 1);
                nearbyX <= Math.Min(board.Width - 1, move.X + 1);
                nearbyX++)
            {
                for (int nearbyY = Math.Max(0, move.Y - 1);
                    nearbyY <= Math.Min(board.Height - 1, move.Y + 1);
                    nearbyY++)
                {
                    if (nearbyX == move.X && nearbyY == move.Y)
                        continue;
                    int value = board.Value(nearbyX, nearbyY);
                    if (value == mark)
                        features[OwnAdjacent] += 1.0 / 8;
                    else if (value == opponentMark)
                        features[OpponentAdjacent] += 1.0 / 8;
                }
            }

            int ownThreatDirections = 0;
            int opponentThreatDirections = 0;
            for (int direction = 0; direction < DirectionX.Length; direction++)
            {
                int dx = DirectionX[direction];
                int dy = DirectionY[direction];
                int ownLength = 1
                    + CountConsecutive(board, move.X, move.Y, dx, dy, mark)
                    + CountConsecutive(board, move.X, move.Y, -dx, -dy, mark);
                int opponentLength = 1
                    + CountConsecutive(board, move.X, move.Y, dx, dy, opponentMark)
                    + CountConsecutive(board, move.X, move.Y, -dx, -dy, opponentMark);
                int ownOpenEnds = CountOpenEnds(board, move.X, move.Y, dx, dy, mark);
                int opponentOpenEnds = CountOpenEnds(board, move.X, move.Y, dx, dy, opponentMark);

                AddLengthFeatures(features, ownLength, OwnTwo, OwnThree, OwnFour);
                AddLengthFeatures(features, opponentLength, OpponentTwo, OpponentThree, OpponentFour);
                features[OwnOpenEnds] += (Math.Min(ownLength, 4) * ownOpenEnds) / 32.0;
                features[OpponentOpenEnds] += (Math.Min(opponentLength, 4) * opponentOpenEnds) / 32.0;

                if (ownLength >= GameBoard.WIN_LENGTH)
                    features[ImmediateWin] = 1;
                if (opponentLength >= GameBoard.WIN_LENGTH)
                    features[ImmediateBlock] = 1;
                if (ownLength >= GameBoard.WIN_LENGTH - 1 && ownOpenEnds > 0)
                    ownThreatDirections++;
                if (opponentLength >= GameBoard.WIN_LENGTH - 1 && opponentOpenEnds > 0)
                    opponentThreatDirections++;
            }

            features[OwnFork] = ownThreatDirections >= 2 ? 1 : 0;
            features[BlockFork] = opponentThreatDirections >= 2 ? 1 : 0;
            return features;
        }

        public static double Score(IReadOnlyList<double> features, IReadOnlyList<double> weights)
        {
            ArgumentNullException.ThrowIfNull(features);
            ArgumentNullException.ThrowIfNull(weights);
            if (features.Count != FeatureNames.Count || weights.Count != FeatureNames.Count)
                throw new ArgumentException("Policy vectors must match the current feature set.");

            double score = 0;
            for (int i = 0; i < features.Count; i++)
                score += features[i] * weights[i];
            return score;
        }

        public static double GetShapedReward(IReadOnlyList<double> features)
        {
            ArgumentNullException.ThrowIfNull(features);
            if (features.Count != FeatureNames.Count)
                throw new ArgumentException("Policy vectors must match the current feature set.", "features");

            return (features[OwnTwo] * 0.01)
                + (features[OwnThree] * 0.03)
                + (features[OwnFour] * 0.06)
                + (features[OpponentTwo] * 0.01)
                + (features[OpponentThree] * 0.04)
                + (features[OpponentFour] * 0.07)
                + (features[ImmediateBlock] * 0.10)
                + (features[OwnFork] * 0.08)
                + (features[BlockFork] * 0.08)
                - 0.001;
        }

        private static void AddLengthFeatures(
            double[] features,
            int length,
            int twoIndex,
            int threeIndex,
            int fourIndex)
        {
            if (length >= 2)
                features[twoIndex] += 0.25;
            if (length >= 3)
                features[threeIndex] += 0.25;
            if (length >= 4)
                features[fourIndex] += 0.25;
        }

        private static int CountConsecutive(GameBoard board, int x, int y, int dx, int dy, int mark)
        {
            int count = 0;
            x += dx;
            y += dy;
            while (board.Value(x, y) == mark)
            {
                count++;
                x += dx;
                y += dy;
            }
            return count;
        }

        private static int CountOpenEnds(GameBoard board, int x, int y, int dx, int dy, int mark)
        {
            int openEnds = 0;
            if (IsOpenEnd(board, x, y, dx, dy, mark))
                openEnds++;
            if (IsOpenEnd(board, x, y, -dx, -dy, mark))
                openEnds++;
            return openEnds;
        }

        private static bool IsOpenEnd(GameBoard board, int x, int y, int dx, int dy, int mark)
        {
            x += dx;
            y += dy;
            while (board.Value(x, y) == mark)
            {
                x += dx;
                y += dy;
            }
            return board.Value(x, y) == 0;
        }

        private static bool IsNearOccupiedCell(GameBoard board, int x, int y)
        {
            for (int nearbyX = Math.Max(0, x - CandidateRadius);
                nearbyX <= Math.Min(board.Width - 1, x + CandidateRadius);
                nearbyX++)
            {
                for (int nearbyY = Math.Max(0, y - CandidateRadius);
                    nearbyY <= Math.Min(board.Height - 1, y + CandidateRadius);
                    nearbyY++)
                {
                    if (board.Value(nearbyX, nearbyY) != 0)
                        return true;
                }
            }
            return false;
        }

        private static int Invert(int mark)
        {
            return mark == ComputerGame.COMPUTER_MARK
                ? ComputerGame.PLAYER_MARK
                : ComputerGame.COMPUTER_MARK;
        }

        private static void ValidateInputs(GameBoard board, int mark, IReadOnlyList<double> weights)
        {
            ArgumentNullException.ThrowIfNull(board);
            ArgumentNullException.ThrowIfNull(weights);
            ValidateMark(mark);
            if (weights.Count != FeatureNames.Count)
                throw new ArgumentException("Policy weights do not match the current feature set.", "weights");
        }

        private static void ValidateMark(int mark)
        {
            if (mark != ComputerGame.PLAYER_MARK && mark != ComputerGame.COMPUTER_MARK)
                throw new ArgumentOutOfRangeException("mark");
        }
    }
}
