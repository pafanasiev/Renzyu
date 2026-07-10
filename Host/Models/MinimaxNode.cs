using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Host.Models
{
    public abstract class MinimaxNode
    {
        public static int TotalChildrenCreated;
        public static Dictionary<int, int> PrunningsByDepth = new Dictionary<int, int>();

        public GameBoard Board { get; set; }
        public int NextMark { get; set; }
        public int Mark { get { return Invert(NextMark); } }
        public double? Rank { get; set; }
        public double? ParentEstimatedRank { get; set; } //for alpha beta prunning
        public int Depth { get; set; }
        public List<MinimaxNode> Children { get; set; }
        //The move that this node represents (the one that is already made)
        public Cell Move { get; private set; }

        /// <summary>
        /// True when the node represents an end game situation (win, lose or draw) 
        /// or if we reached the max allowed depth in minimax tree
        /// </summary>
        public bool IsLeave
        {
            get
            {
                return Depth == AI.maxDepth || Rank == AI.maxRank || Rank == AI.minRank;
            }
        }

        public MinimaxNode(Cell move, GameBoard board)
        {
            Children = new List<MinimaxNode>();
            Move = move;
            Board = board;
        }

        public MinimaxNode CreateChild(Cell move)
        {
            var child = GetChildInstance(move, Board.Copy());
            child.Depth = Depth + 1;
            child.ParentEstimatedRank = Rank;
            child.Board.Move(move.X, move.Y, NextMark);
            Children.Add(child);
            TotalChildrenCreated++;
            return child;
        }

        public override string ToString()
        {
            return String.Format("X: {0}, Y: {1}, depth: {2}, rank: {3}", Move.X, Move.Y, Depth, Rank);
        }
        protected abstract MinimaxNode GetChildInstance(Cell c, GameBoard board);
        private int Invert(int mark)
        {
            if (mark == ComputerGame.COMPUTER_MARK) return ComputerGame.PLAYER_MARK;
            else return ComputerGame.COMPUTER_MARK;
        }

        internal abstract void UpdateEstimatedRank(MinimaxNode child);
        public abstract bool IsDeadEnd();
    }

    //represents board on which human just made the move.
    public class MaxNode : MinimaxNode
    {
        public MaxNode(Cell c, GameBoard board)
            : base(c, board)
        {
            this.NextMark = ComputerGame.COMPUTER_MARK;
        }

        protected override MinimaxNode GetChildInstance(Cell c, GameBoard board)
        {
            return new MinNode(c, board);
        }

        internal override void UpdateEstimatedRank(MinimaxNode child)
        {
            if (child.Rank.HasValue)
            {
                if (Rank.HasValue)
                {
                    Rank = Math.Max(Rank.Value, child.Rank.Value);
                }
                else 
                {
                    Rank = child.Rank;
                }
            }
        }

        public override bool IsDeadEnd()
        {
            return Rank >= ParentEstimatedRank;
        }
    }

    //represents board on which computer just made the move
    public class MinNode : MinimaxNode
    {
        public MinNode(Cell c, GameBoard board)
            : base(c, board)
        {
            this.NextMark = ComputerGame.PLAYER_MARK;
        }

        protected override MinimaxNode GetChildInstance(Cell c, GameBoard board)
        {
            return new MaxNode(c, board);
        }

        internal override void UpdateEstimatedRank(MinimaxNode child)
        {
            if (child.Rank.HasValue)
            {
                if (Rank.HasValue)
                {
                    Rank = Math.Min(Rank.Value, child.Rank.Value);
                }
                else
                {
                    Rank = child.Rank;
                }
            }
        }

        public override bool IsDeadEnd()
        {
            return Rank <= ParentEstimatedRank;
        }
    }
}