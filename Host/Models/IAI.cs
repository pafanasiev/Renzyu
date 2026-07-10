using System;
namespace Host.Models
{
    interface IAI
    {
        Cell GetBestMove(GameBoard board);
        void EvaluateNodeRank(MinimaxNode current);
    }
}
