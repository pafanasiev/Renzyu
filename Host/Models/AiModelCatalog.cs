using System.Text.Json;

namespace Host.Models
{
    public sealed class AiModelDescriptor
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public bool IsBuiltIn { get; init; }
        public int EpisodesTrained { get; init; }
        public DateTimeOffset? CreatedAtUtc { get; init; }
        public int EvaluationGames { get; init; }
        public double EvaluationScore { get; init; }
        public int OpponentDepth { get; init; }
    }

    public interface IAiModelCatalog
    {
        IReadOnlyList<AiModelDescriptor> GetAvailableModels();
        IAI CreateAI(string modelId);
    }

    public sealed class FileAiModelCatalog : IAiModelCatalog
    {
        public const string MinimaxModelId = "minimax";

        private readonly string _modelDirectory;
        private readonly ILogger<FileAiModelCatalog> _logger;

        public FileAiModelCatalog(string modelDirectory, ILogger<FileAiModelCatalog> logger)
        {
            if (string.IsNullOrWhiteSpace(modelDirectory))
                throw new ArgumentException("A model directory is required.", "modelDirectory");

            _modelDirectory = Path.GetFullPath(modelDirectory);
            _logger = logger ?? throw new ArgumentNullException("logger");
        }

        public IReadOnlyList<AiModelDescriptor> GetAvailableModels()
        {
            var models = new List<AiModelDescriptor>
            {
                new AiModelDescriptor
                {
                    Id = MinimaxModelId,
                    Name = "Minimax (built in)",
                    IsBuiltIn = true,
                },
            };

            models.AddRange(LoadModels()
                .Values
                .OrderByDescending(entry => entry.Model.CreatedAtUtc)
                .Select(entry => new AiModelDescriptor
                {
                    Id = entry.Model.Id,
                    Name = entry.Model.Name,
                    EpisodesTrained = entry.Model.EpisodesTrained,
                    CreatedAtUtc = entry.Model.CreatedAtUtc,
                    EvaluationGames = entry.Model.Evaluation.Games,
                    EvaluationScore = entry.Model.Evaluation.Score,
                    OpponentDepth = entry.Model.Training.OpponentDepth,
                }));

            return models;
        }

        public IAI CreateAI(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId) || modelId == MinimaxModelId)
                return new AI();

            var models = LoadModels();
            if (!models.TryGetValue(modelId, out var entry))
                throw new KeyNotFoundException("AI model '" + modelId + "' is not available.");

            return new ReinforcementAI(entry.Model, _logger);
        }

        private Dictionary<string, ModelFile> LoadModels()
        {
            var result = new Dictionary<string, ModelFile>(StringComparer.Ordinal);
            if (!Directory.Exists(_modelDirectory))
                return result;

            foreach (string path in Directory.EnumerateFiles(_modelDirectory, "*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                try
                {
                    var model = ReinforcementModelStore.Load(path);
                    if (model.Id == MinimaxModelId)
                    {
                        _logger.LogWarning(
                            "Ignoring AI model {ModelFile} because its id is reserved.",
                            Path.GetFileName(path));
                        continue;
                    }
                    if (!result.TryAdd(model.Id, new ModelFile(model, path)))
                    {
                        _logger.LogWarning(
                            "Ignoring duplicate AI model id {ModelId} in {ModelFile}.",
                            model.Id,
                            Path.GetFileName(path));
                    }
                }
                catch (Exception exception) when (
                    exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is JsonException
                    || exception is InvalidDataException)
                {
                    _logger.LogWarning(
                        exception,
                        "Ignoring invalid AI model file {ModelFile}.",
                        Path.GetFileName(path));
                }
            }

            return result;
        }

        private sealed class ModelFile
        {
            public ModelFile(ReinforcementModel model, string path)
            {
                Model = model;
                Path = path;
            }

            public ReinforcementModel Model { get; }
            public string Path { get; }
        }
    }
}
