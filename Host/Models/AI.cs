using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace Host.Models
{
    public class AI : IAI
    {
        public const int maxDepth = 2;
        public const int maxRank = 100000;
        public const int minRank = -100000;

        public Cell GetBestMove(GameBoard board)
        {
            var root = new MaxNode(board.LastMove, board);
            ProcessSolutionTree(root);
            var bestMove = root.Children.First(node => node.Rank == root.Rank);
            return bestMove.Move;
        }

        private void ProcessSolutionTree(MinimaxNode current)
        {
            //evaluate for win condition first because if true -> we can save resources and not process childs.
            EvaluateNodeRank(current);

            //this is a leave so no need to process children
            if (current.IsLeave)
                return;

            List<Cell> freeCells = current.Board.GetAvailableCells();
            foreach (var cell in freeCells)
            {
                MinimaxNode child = current.CreateChild(cell);
                if (child.Move.X == 4 && child.Move.Y == 5)
                    Debug.Write("");
                ProcessSolutionTree(child);
                current.UpdateEstimatedRank(child);
                if (current.IsDeadEnd())
                    break;            //perform alpha beta prunning
            }

            //release memory used up by board
            current.Board = null;
        }

        public void EvaluateNodeRank(MinimaxNode current)
        {
            //if we reached maxDepth and the current situation is not an endgame (win/lose/draw)
            if (current.Depth == maxDepth)
            {
                //current.Rank = 0; //the most neutral rank
                double rank = 0;
                var analyzer = new GameBoardAnalyzer(current.Board);
                var stats = analyzer.GetMoveStats(ComputerGame.COMPUTER_MARK);
                rank += 15000 * stats.Count(r => r.MaxSeries == 5 && !r.IsBroken);
                rank += 15000 * stats.Count(r => r.MaxSeries == 5 && r.IsBroken);
                rank += 2000 * stats.Count(r => r.MaxSeries == 4 && !r.IsCapped && !r.IsBroken);
                rank += 2000 * stats.Count(r => r.MaxSeries == 5 && r.IsBroken && !r.IsCapped);
                rank += 500 * stats.Count(r => r.MaxSeries == 5 && r.IsBroken && r.IsCapped);
                rank += 500 * stats.Count(r => r.MaxSeries == 4 && r.IsCapped && !r.IsBroken);
                rank += 500 * stats.Count(r => r.MaxSeries == 4 && !r.IsCapped && r.IsBroken);
                rank += 400 * stats.Count(r => r.MaxSeries == 4 && r.IsCapped && r.IsBroken);
                rank += 130 * stats.Count(r => r.MaxSeries == 3 && !r.IsCapped && !r.IsBroken);
                rank += 80 * stats.Count(r => r.MaxSeries == 3 && !r.IsCapped && r.IsBroken);
                rank += 20 * stats.Count(r => r.MaxSeries == 3 && r.IsCapped && !r.IsBroken);
                rank += 15 * stats.Count(r => r.MaxSeries == 3 && r.IsCapped && r.IsBroken);
                rank += 10 * stats.Count(r => r.MaxSeries == 2 && !r.IsCapped && !r.IsBroken);
                rank += 9 * stats.Count(r => r.MaxSeries == 2 && !r.IsCapped && r.IsBroken);
                rank += 8 * stats.Count(r => r.MaxSeries == 2 && r.IsCapped && r.IsBroken);

                var opponentStats = analyzer.GetMoveStats(ComputerGame.PLAYER_MARK);
                rank += -14000 * opponentStats.Count(r => r.MaxSeries == 5 && !r.IsBroken);
                rank += -14000 * opponentStats.Count(r => r.MaxSeries == 5 && r.IsBroken);
                rank += -2600 * opponentStats.Count(r => r.MaxSeries == 4 && !r.IsCapped && !r.IsBroken);
                rank += -2600 * opponentStats.Count(r => r.MaxSeries == 5 && r.IsBroken && !r.IsCapped);
                rank += -500 * opponentStats.Count(r => r.MaxSeries == 5 && r.IsBroken && r.IsCapped);
                rank += -500 * opponentStats.Count(r => r.MaxSeries == 4 && r.IsCapped && !r.IsBroken);
                rank += -500 * opponentStats.Count(r => r.MaxSeries == 4 && !r.IsCapped && r.IsBroken);
                rank += -400 * opponentStats.Count(r => r.MaxSeries == 4 && r.IsCapped && r.IsBroken);
                rank += -120 * opponentStats.Count(r => r.MaxSeries == 3 && !r.IsCapped && !r.IsBroken);
                rank += -80 * opponentStats.Count(r => r.MaxSeries == 3 && !r.IsCapped && r.IsBroken);
                rank += -20 * opponentStats.Count(r => r.MaxSeries == 3 && r.IsCapped && !r.IsBroken);
                rank += -15 * opponentStats.Count(r => r.MaxSeries == 3 && r.IsCapped && r.IsBroken);
                rank += -10 * opponentStats.Count(r => r.MaxSeries == 2 && !r.IsCapped && !r.IsBroken);
                rank += -9 * opponentStats.Count(r => r.MaxSeries == 2 && !r.IsCapped && r.IsBroken);
                rank += -8 * opponentStats.Count(r => r.MaxSeries == 2 && r.IsCapped && r.IsBroken);


                current.Rank = rank;
            }
            //we don't assign any rank if it's not a leave - it will be assigned once all children are processed according to minimax algo
            //(max nodes will take max rank of their children and min nodes - min rank of their children)
        }
    }
}