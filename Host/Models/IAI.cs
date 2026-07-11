using System;
namespace Host.Models
{
    public interface IAI
    {
        Cell GetBestMove(GameBoard board);
        void EvaluateNodeRank(MinimaxNode current);
    }
}
