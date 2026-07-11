namespace Host.Models
{
    public sealed class GamePageViewModel
    {
        public Guid? Token { get; init; }
        public IReadOnlyList<AiModelDescriptor> AiModels { get; init; }
        public string DefaultAiModelId { get; init; }
    }
}
