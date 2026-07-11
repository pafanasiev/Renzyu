using System.Globalization;

namespace Renzyu.Training
{
    internal sealed class TrainingOptions
    {
        public string Name { get; private set; } = "RL agent " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        public int Episodes { get; private set; } = 500;
        public double LearningRate { get; private set; } = 0.04;
        public double DiscountFactor { get; private set; } = 0.90;
        public double InitialEpsilon { get; private set; } = 0.35;
        public double FinalEpsilon { get; private set; } = 0.03;
        public int OpponentDepth { get; private set; } = 4;
        public int OpponentMaxNodes { get; private set; } = 60000;
        public int TeacherDepth { get; private set; } = 6;
        public int TeacherMaxNodes { get; private set; } = 120000;
        public int EvaluateEvery { get; private set; } = 25;
        public int EvaluationGames { get; private set; } = 2;
        public int CheckpointEvery { get; private set; } = 100;
        public int TimeLimitSeconds { get; private set; } = 300;
        public double TargetScore { get; private set; } = 0.75;
        public int Seed { get; private set; } = 1337;
        public string OutputDirectory { get; private set; } = FindDefaultOutputDirectory();
        public bool Dashboard { get; private set; } = true;
        public bool ShowHelp { get; private set; }

        public static TrainingOptions Parse(string[] args)
        {
            var result = new TrainingOptions();
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                switch (argument)
                {
                    case "--name":
                        result.Name = ReadValue(args, ref index, argument);
                        break;
                    case "--episodes":
                        result.Episodes = ReadInt(args, ref index, argument);
                        break;
                    case "--learning-rate":
                        result.LearningRate = ReadDouble(args, ref index, argument);
                        break;
                    case "--discount":
                        result.DiscountFactor = ReadDouble(args, ref index, argument);
                        break;
                    case "--epsilon-start":
                        result.InitialEpsilon = ReadDouble(args, ref index, argument);
                        break;
                    case "--epsilon-end":
                        result.FinalEpsilon = ReadDouble(args, ref index, argument);
                        break;
                    case "--opponent-depth":
                        result.OpponentDepth = ReadInt(args, ref index, argument);
                        break;
                    case "--opponent-nodes":
                        result.OpponentMaxNodes = ReadInt(args, ref index, argument);
                        break;
                    case "--teacher-depth":
                        result.TeacherDepth = ReadInt(args, ref index, argument);
                        break;
                    case "--teacher-nodes":
                        result.TeacherMaxNodes = ReadInt(args, ref index, argument);
                        break;
                    case "--evaluate-every":
                        result.EvaluateEvery = ReadInt(args, ref index, argument);
                        break;
                    case "--evaluation-games":
                        result.EvaluationGames = ReadInt(args, ref index, argument);
                        break;
                    case "--checkpoint-every":
                        result.CheckpointEvery = ReadInt(args, ref index, argument);
                        break;
                    case "--time-limit-seconds":
                        result.TimeLimitSeconds = ReadInt(args, ref index, argument);
                        break;
                    case "--target-score":
                        result.TargetScore = ReadDouble(args, ref index, argument);
                        break;
                    case "--seed":
                        result.Seed = ReadInt(args, ref index, argument);
                        break;
                    case "--output":
                        result.OutputDirectory = Path.GetFullPath(ReadValue(args, ref index, argument));
                        break;
                    case "--no-dashboard":
                        result.Dashboard = false;
                        break;
                    case "--help":
                    case "-h":
                        result.ShowHelp = true;
                        break;
                    default:
                        throw new ArgumentException("Unknown option: " + argument + ".");
                }
            }

            result.Validate();
            return result;
        }

        private void Validate()
        {
            if (ShowHelp)
                return;
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 120)
                throw new ArgumentException("--name must contain between 1 and 120 characters.");
            if (Episodes < 1)
                throw new ArgumentException("--episodes must be at least 1.");
            if (LearningRate <= 0 || LearningRate > 1)
                throw new ArgumentException("--learning-rate must be greater than 0 and no greater than 1.");
            if (DiscountFactor < 0 || DiscountFactor > 1)
                throw new ArgumentException("--discount must be between 0 and 1.");
            if (InitialEpsilon < 0 || InitialEpsilon > 1 || FinalEpsilon < 0 || FinalEpsilon > 1)
                throw new ArgumentException("Epsilon values must be between 0 and 1.");
            if (FinalEpsilon > InitialEpsilon)
                throw new ArgumentException("--epsilon-end cannot be greater than --epsilon-start.");
            if (OpponentDepth < 1 || OpponentDepth > 4)
                throw new ArgumentException("--opponent-depth must be between 1 and 4.");
            if (OpponentMaxNodes < 1)
                throw new ArgumentException("--opponent-nodes must be at least 1.");
            if (TeacherDepth < 1 || TeacherDepth > 6)
                throw new ArgumentException("--teacher-depth must be between 1 and 6.");
            if (TeacherMaxNodes < 1)
                throw new ArgumentException("--teacher-nodes must be at least 1.");
            if (EvaluateEvery < 1)
                throw new ArgumentException("--evaluate-every must be at least 1.");
            if (EvaluationGames < 2)
                throw new ArgumentException("--evaluation-games must be at least 2 to evaluate both sides.");
            if (CheckpointEvery < 0)
                throw new ArgumentException("--checkpoint-every cannot be negative.");
            if (TimeLimitSeconds < 1 || TimeLimitSeconds > 300)
                throw new ArgumentException("--time-limit-seconds must be between 1 and 300.");
            if (TargetScore <= 0.5 || TargetScore > 1)
                throw new ArgumentException("--target-score must be greater than 0.5 and no greater than 1.");
        }

        private static string ReadValue(string[] args, ref int index, string option)
        {
            index++;
            if (index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(option + " requires a value.");
            return args[index];
        }

        private static int ReadInt(string[] args, ref int index, string option)
        {
            string value = ReadValue(args, ref index, option);
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new ArgumentException(option + " requires an integer.");
            return result;
        }

        private static double ReadDouble(string[] args, ref int index, string option)
        {
            string value = ReadValue(args, ref index, option);
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
                || !double.IsFinite(result))
            {
                throw new ArgumentException(option + " requires a finite number.");
            }
            return result;
        }

        private static string FindDefaultOutputDirectory()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Renzyu.sln")))
                    return Path.Combine(directory.FullName, "Host", "TrainedModels");
                directory = directory.Parent;
            }

            return Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Host",
                "TrainedModels"));
        }
    }
}
