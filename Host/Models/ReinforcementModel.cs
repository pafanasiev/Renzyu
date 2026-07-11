using System.Text;
using System.Text.Json;

namespace Host.Models
{
    public sealed class ReinforcementModel
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public int BoardSize { get; set; } = GameBoard.SIZE;
        public int WinLength { get; set; } = GameBoard.WIN_LENGTH;
        public int EpisodesTrained { get; set; }
        public string[] FeatureNames { get; set; }
        public double[] Weights { get; set; }
        public List<ReinforcementPolicyEntry> PolicyEntries { get; set; } =
            new List<ReinforcementPolicyEntry>();
        public ReinforcementTrainingMetadata Training { get; set; }
        public ReinforcementEvaluation Evaluation { get; set; }

        public void Validate()
        {
            if (SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported model schema version: " + SchemaVersion + ".");
            if (string.IsNullOrWhiteSpace(Id) || Id.Length > 120)
                throw new InvalidDataException("The model id must contain between 1 and 120 characters.");
            if (Id.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
            {
                throw new InvalidDataException("The model id must contain only ASCII letters, digits, hyphens, or underscores.");
            }
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 120)
                throw new InvalidDataException("The model name must contain between 1 and 120 characters.");
            if (CreatedAtUtc == default)
                throw new InvalidDataException("The model creation timestamp is required.");
            if (BoardSize != GameBoard.SIZE || WinLength != GameBoard.WIN_LENGTH)
                throw new InvalidDataException("The model is not compatible with this board.");
            if (EpisodesTrained < 0)
                throw new InvalidDataException("Episodes trained cannot be negative.");
            if (FeatureNames == null
                || !FeatureNames.SequenceEqual(LearnedPolicy.FeatureNames, StringComparer.Ordinal))
            {
                throw new InvalidDataException("The model feature set is not compatible with this application.");
            }
            if (Weights == null || Weights.Length != LearnedPolicy.FeatureNames.Count)
                throw new InvalidDataException("The model has an invalid number of weights.");
            if (Weights.Any(weight => !double.IsFinite(weight)))
                throw new InvalidDataException("Model weights must be finite numbers.");
            if (PolicyEntries == null)
                throw new InvalidDataException("Model policy entries are required.");
            var policyStates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in PolicyEntries)
            {
                if (entry == null)
                    throw new InvalidDataException("Model policy entries cannot be null.");
                entry.Validate(BoardSize);
                if (!policyStates.Add(entry.StateKey))
                    throw new InvalidDataException("Model policy states must be unique.");
            }
            if (Training == null)
                throw new InvalidDataException("Training metadata is required.");
            if (Evaluation == null)
                throw new InvalidDataException("Evaluation metadata is required.");

            Evaluation.Validate();
        }
    }

    public sealed class ReinforcementTrainingMetadata
    {
        public string Opponent { get; set; } = "minimax";
        public int OpponentDepth { get; set; }
        public int OpponentMaxNodes { get; set; }
        public int TeacherDepth { get; set; }
        public int TeacherMaxNodes { get; set; }
        public double LearningRate { get; set; }
        public double DiscountFactor { get; set; }
        public double InitialEpsilon { get; set; }
        public double FinalEpsilon { get; set; }
        public int TimeLimitSeconds { get; set; }
        public double TargetScore { get; set; }
        public bool TargetReached { get; set; }
        public int Seed { get; set; }
    }

    public sealed class ReinforcementPolicyEntry
    {
        public string StateKey { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public double Value { get; set; }
        public int Visits { get; set; }

        public void Validate(int boardSize)
        {
            if (string.IsNullOrEmpty(StateKey)
                || StateKey.Length != 64
                || StateKey.Any(character =>
                    !((character >= '0' && character <= '9')
                        || (character >= 'A' && character <= 'F'))))
            {
                throw new InvalidDataException("Policy state keys must be uppercase SHA-256 hashes.");
            }
            if (X < 0 || X >= boardSize || Y < 0 || Y >= boardSize)
                throw new InvalidDataException("Policy moves must be on the board.");
            if (!double.IsFinite(Value))
                throw new InvalidDataException("Policy values must be finite numbers.");
            if (Visits < 1)
                throw new InvalidDataException("Policy visits must be positive.");
        }
    }

    public sealed class ReinforcementEvaluation
    {
        public int Games { get; set; }
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }

        public double Score
        {
            get
            {
                return Games == 0 ? 0 : (Wins + (Draws * 0.5)) / Games;
            }
        }

        public void Validate()
        {
            if (Games < 0 || Wins < 0 || Draws < 0 || Losses < 0)
                throw new InvalidDataException("Evaluation counts cannot be negative.");
            if (Wins + Draws + Losses != Games)
                throw new InvalidDataException("Evaluation counts do not add up to the number of games.");
        }
    }

    public static class ReinforcementModelStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
        };

        public static ReinforcementModel Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A model path is required.", "path");

            using var stream = File.OpenRead(path);
            var model = JsonSerializer.Deserialize<ReinforcementModel>(stream, SerializerOptions);
            if (model == null)
                throw new InvalidDataException("The model file is empty.");

            model.Validate();
            return model;
        }

        public static void Save(ReinforcementModel model, string path)
        {
            ArgumentNullException.ThrowIfNull(model);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A model path is required.", "path");

            model.Validate();
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The model path must have a parent directory.");

            Directory.CreateDirectory(directory);
            var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temporaryPath))
                {
                    JsonSerializer.Serialize(stream, model, SerializerOptions);
                }
                File.Move(temporaryPath, fullPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "model";

            var result = new StringBuilder();
            var needsSeparator = false;
            foreach (char character in value.Trim().ToLowerInvariant())
            {
                if (char.IsAsciiLetterOrDigit(character))
                {
                    if (needsSeparator && result.Length > 0)
                        result.Append('-');
                    result.Append(character);
                    needsSeparator = false;
                }
                else
                {
                    needsSeparator = true;
                }
            }

            return result.Length == 0 ? "model" : result.ToString();
        }
    }
}
