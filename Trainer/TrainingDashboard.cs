using System.Text;
using Host.Models;

namespace Renzyu.Training
{
    internal sealed class TrainingDashboard : IDisposable
    {
        private const string Reset = "\u001b[0m";
        private const string Cyan = "\u001b[36m";
        private const string Green = "\u001b[32m";
        private const string Red = "\u001b[31m";
        private const string Yellow = "\u001b[33m";
        private const string Dim = "\u001b[2m";
        private const string EvaluationScale = " .:-=+*#%@";

        private readonly string modelName;
        private readonly bool interactive;
        private DateTime lastRenderUtc = DateTime.MinValue;
        private bool disposed;

        public TrainingDashboard(string modelName, bool enabled)
        {
            this.modelName = modelName;
            interactive = enabled && !Console.IsOutputRedirected;
            if (interactive)
                Console.Write("\u001b[?25l");
        }

        public void Render(TrainingSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            bool importantUpdate = snapshot.IsComplete
                || snapshot.CheckpointPath != null
                || snapshot.EvaluationHistory.Count > 0
                    && snapshot.Episode % Math.Max(1, snapshot.TotalEpisodes / 20) == 0;

            if (interactive)
            {
                if (!importantUpdate
                    && DateTime.UtcNow - lastRenderUtc < TimeSpan.FromMilliseconds(100))
                {
                    return;
                }
                lastRenderUtc = DateTime.UtcNow;
                Console.Write("\u001b[2J\u001b[H");
                Console.Write(BuildDashboard(snapshot, true));
            }
            else if (importantUpdate)
            {
                Console.WriteLine(BuildLogLine(snapshot));
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (interactive)
                Console.Write(Reset + "\u001b[?25h");
        }

        private string BuildDashboard(TrainingSnapshot snapshot, bool useColor)
        {
            var output = new StringBuilder();
            string titleColor = useColor ? Cyan : "";
            string reset = useColor ? Reset : "";
            output.AppendLine(titleColor + "RENZYU RL ARENA" + reset + "  " + modelName);
            output.AppendLine(new string('=', 72));

            double progress = snapshot.TotalEpisodes == 0
                ? 0
                : snapshot.Episode / (double)snapshot.TotalEpisodes;
            output.Append("  ");
            output.Append(BuildProgressBar(progress, 42));
            output.Append(' ');
            output.Append(progress.ToString("P1").PadLeft(6));
            output.Append("  episode ");
            output.Append(snapshot.Episode);
            output.Append('/');
            output.AppendLine(snapshot.TotalEpisodes.ToString());

            double episodesPerSecond = snapshot.Elapsed.TotalSeconds <= 0
                ? 0
                : snapshot.Episode / snapshot.Elapsed.TotalSeconds;
            TimeSpan eta = episodesPerSecond <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds((snapshot.TotalEpisodes - snapshot.Episode) / episodesPerSecond);
            output.Append("  training W/D/L  ");
            output.Append(snapshot.Wins);
            output.Append('/');
            output.Append(snapshot.Draws);
            output.Append('/');
            output.Append(snapshot.Losses);
            output.Append("    epsilon ");
            output.Append(snapshot.Epsilon.ToString("0.000"));
            output.Append("    speed ");
            output.Append(episodesPerSecond.ToString("0.0"));
            output.AppendLine(" eps/s");
            output.Append("  last ");
            output.Append(snapshot.LastOutcome.ToString().ToLowerInvariant().PadRight(5));
            output.Append("  reward ");
            output.Append(snapshot.LastReward.ToString("+0.000;-0.000;0.000"));
            output.Append("  |TD| ");
            output.Append(snapshot.MeanAbsoluteTdError.ToString("0.000"));
            output.Append("  moves ");
            output.Append(snapshot.Moves);
            output.Append("  ETA ");
            output.AppendLine(FormatDuration(eta));

            output.AppendLine();
            output.Append(useColor ? Yellow : "");
            output.Append("  MINIMAX EVALUATION  ");
            output.Append(useColor ? reset : "");
            if (snapshot.Evaluation.Games == 0)
            {
                output.AppendLine("waiting for first evaluation...");
            }
            else
            {
                output.Append("W/D/L ");
                output.Append(snapshot.Evaluation.Wins);
                output.Append('/');
                output.Append(snapshot.Evaluation.Draws);
                output.Append('/');
                output.Append(snapshot.Evaluation.Losses);
                output.Append("   score ");
                output.Append(snapshot.Evaluation.Score.ToString("P1"));
                output.Append("   target ");
                output.Append(snapshot.TargetScore.ToString("P0"));
                output.Append(snapshot.TargetReached ? " PASS" : " ...");
                output.Append("   trend [");
                output.Append(BuildEvaluationTrend(snapshot.EvaluationHistory));
                output.AppendLine("]");
            }

            output.AppendLine();
            output.Append("  distilled winning positions: ");
            output.AppendLine(snapshot.PolicyPositions.ToString());
            output.AppendLine();
            output.AppendLine("  STRONGEST LEARNED WEIGHTS");
            foreach (var weight in LearnedPolicy.FeatureNames
                .Select((name, index) => new { Name = name, Value = snapshot.Weights[index] })
                .OrderByDescending(item => Math.Abs(item.Value))
                .Take(5))
            {
                output.Append("  ");
                output.Append(weight.Name.PadRight(22));
                output.Append(weight.Value.ToString("+0.000;-0.000;0.000").PadLeft(8));
                output.Append("  ");
                output.AppendLine(BuildWeightBar(weight.Value, 18));
            }

            output.AppendLine();
            output.AppendLine("  LAST TRAINING BOARD   X minimax   O learner   lowercase = last move");
            output.Append(BuildBoard(snapshot.Board, snapshot.LastMove, useColor));

            if (snapshot.CheckpointPath != null)
            {
                output.AppendLine();
                output.AppendLine("  checkpoint: " + snapshot.CheckpointPath);
            }
            if (snapshot.IsComplete)
            {
                output.AppendLine();
                output.Append("  ");
                output.Append(snapshot.TargetReached
                    ? "BENCHMARK PASSED - best model preserved"
                    : snapshot.IsCancelled
                        ? "TRAINING STOPPED - best model preserved"
                        : "TRAINING COMPLETE - benchmark not reached");
                output.Append(" in ");
                output.AppendLine(FormatDuration(snapshot.Elapsed));
            }
            return output.ToString();
        }

        private static string BuildLogLine(TrainingSnapshot snapshot)
        {
            return "episode "
                + snapshot.Episode
                + "/"
                + snapshot.TotalEpisodes
                + " train W/D/L "
                + snapshot.Wins
                + "/"
                + snapshot.Draws
                + "/"
                + snapshot.Losses
                + " eval "
                + snapshot.Evaluation.Score.ToString("P1")
                + " target "
                + snapshot.TargetScore.ToString("P0")
                + (snapshot.TargetReached ? " PASS" : "")
                + " policy "
                + snapshot.PolicyPositions
                + " epsilon "
                + snapshot.Epsilon.ToString("0.000")
                + (snapshot.CheckpointPath == null ? "" : " checkpoint " + snapshot.CheckpointPath);
        }

        private static string BuildProgressBar(double progress, int width)
        {
            int completed = Math.Clamp((int)Math.Round(progress * width), 0, width);
            return "[" + new string('#', completed) + new string('-', width - completed) + "]";
        }

        private static string BuildEvaluationTrend(IReadOnlyList<double> history)
        {
            if (history.Count == 0)
                return "";

            int start = Math.Max(0, history.Count - 24);
            var output = new StringBuilder();
            for (int index = start; index < history.Count; index++)
            {
                int scaleIndex = Math.Clamp(
                    (int)Math.Round(history[index] * (EvaluationScale.Length - 1)),
                    0,
                    EvaluationScale.Length - 1);
                output.Append(EvaluationScale[scaleIndex]);
            }
            return output.ToString();
        }

        private static string BuildWeightBar(double value, int width)
        {
            int magnitude = Math.Clamp(
                (int)Math.Round(Math.Abs(value) / 4 * width),
                0,
                width);
            char fill = value < 0 ? '<' : '>';
            return new string(fill, magnitude).PadRight(width, '.');
        }

        private static string BuildBoard(int[,] board, Cell lastMove, bool useColor)
        {
            var output = new StringBuilder();
            int width = board.GetLength(0);
            int height = board.GetLength(1);
            output.Append("     ");
            for (int x = 0; x < width; x++)
                output.Append((x % 10) + " ");
            output.AppendLine();

            for (int y = 0; y < height; y++)
            {
                output.Append("  ");
                output.Append(y.ToString("D2"));
                output.Append(" ");
                for (int x = 0; x < width; x++)
                {
                    int value = board[x, y];
                    bool isLast = lastMove != null && lastMove.X == x && lastMove.Y == y;
                    char mark = value == ComputerGame.PLAYER_MARK
                        ? (isLast ? 'x' : 'X')
                        : value == ComputerGame.COMPUTER_MARK
                            ? (isLast ? 'o' : 'O')
                            : '.';
                    if (useColor && value != 0)
                    {
                        output.Append(value == ComputerGame.COMPUTER_MARK ? Green : Red);
                        if (isLast)
                            output.Append(Yellow);
                    }
                    else if (useColor && value == 0)
                    {
                        output.Append(Dim);
                    }
                    output.Append(mark);
                    output.Append(useColor ? Reset : "");
                    output.Append(' ');
                }
                output.AppendLine();
            }
            return output.ToString();
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return duration.ToString(@"h\:mm\:ss");
            return duration.ToString(@"m\:ss");
        }
    }
}
