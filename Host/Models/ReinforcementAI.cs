namespace Host.Models
{
    public sealed class ReinforcementAI : IAI
    {
        private readonly double[] _weights;
        private readonly Dictionary<string, ReinforcementPolicyEntry> _policyEntries;
        private readonly ILogger _logger;

        public ReinforcementAI(ReinforcementModel model, ILogger logger = null)
        {
            ArgumentNullException.ThrowIfNull(model);
            model.Validate();
            _weights = (double[])model.Weights.Clone();
            _logger = logger;
            _policyEntries = model.PolicyEntries.ToDictionary(
                entry => entry.StateKey,
                StringComparer.Ordinal);
        }

        public Cell GetBestMove(GameBoard board)
        {
            return GetBestMove(board, ComputerGame.COMPUTER_MARK);
        }

        public Cell GetBestMove(GameBoard board, int mark)
        {
            string stateKey = LearnedPolicy.GetStateKey(board, mark);
            if (_policyEntries.TryGetValue(stateKey, out var entry))
            {
                if (board.Value(entry.X, entry.Y) == 0)
                    return new Cell(entry.X, entry.Y);

                _logger?.LogWarning(
                    "Ignoring an illegal distilled move for policy state {PolicyState}.",
                    stateKey);
                _policyEntries.Remove(stateKey);
            }

            return LearnedPolicy
                .SelectMove(board, mark, _weights)
                .Move;
        }

        public void EvaluateNodeRank(MinimaxNode current)
        {
            throw new NotSupportedException("Reinforcement policies do not evaluate minimax nodes.");
        }
    }
}
