using System;

namespace Host.Models
{
    public class AI : IAI
    {
        public const int maxDepth = 2;
        public const int maxRank = 100000;
        public const int minRank = -100000;

        private const int MaxHeuristicRank = 50000;
        private const int CandidateRadius = 2;
        private static readonly int[] ComputerWeights = { 0, 2, 12, 180, 6000, 0 };
        private static readonly int[] PlayerWeights = { 0, 2, 12, 180, 6500, 0 };
        private static readonly int[] DirectionX = { 1, 0, 1, 1 };
        private static readonly int[] DirectionY = { 0, 1, 1, -1 };

        public Cell GetBestMove(GameBoard board)
        {
            if (board == null)
                throw new ArgumentNullException("board");
            if (board.Width == 0 || board.Height == 0)
                throw new InvalidOperationException("No legal moves are available.");

            if (!HasOccupiedCells(board))
            {
                return new Cell(board.Width / 2, board.Height / 2);
            }

            var search = new SearchBuffers(board.Width * board.Height);
            int moveCount = GenerateMoves(board, search, 0, ComputerGame.COMPUTER_MARK);
            if (moveCount == 0)
                throw new InvalidOperationException("No legal moves are available.");

            int bestMove = search.Moves[0][moveCount - 1];
            int bestRank = minRank;
            int alpha = minRank;

            for (int i = moveCount - 1; i >= 0; i--)
            {
                int encodedMove = search.Moves[0][i];
                int x = encodedMove / board.Height;
                int y = encodedMove % board.Height;
                int rank;

                board.SetValue(x, y, ComputerGame.COMPUTER_MARK);
                try
                {
                    rank = Search(
                        board,
                        search,
                        1,
                        false,
                        alpha,
                        maxRank,
                        x,
                        y,
                        ComputerGame.COMPUTER_MARK);
                }
                finally
                {
                    board.SetValue(x, y, 0);
                }

                if (rank > bestRank)
                {
                    bestRank = rank;
                    bestMove = encodedMove;
                }

                if (bestRank > alpha)
                    alpha = bestRank;

                if (bestRank == maxRank - 1)
                    break;
            }

            return new Cell(bestMove / board.Height, bestMove % board.Height);
        }

        private int Search(
            GameBoard board,
            SearchBuffers search,
            int depth,
            bool maximizing,
            int alpha,
            int beta,
            int lastX,
            int lastY,
            int lastMark)
        {
            if (IsWinningMove(board, lastX, lastY, lastMark))
                return lastMark == ComputerGame.COMPUTER_MARK ? maxRank - depth : minRank + depth;

            if (depth == maxDepth)
                return EvaluateLeaf(board, maximizing, depth);

            int mark = maximizing ? ComputerGame.COMPUTER_MARK : ComputerGame.PLAYER_MARK;
            int moveCount = GenerateMoves(board, search, depth, mark);
            if (moveCount == 0)
                return EvaluateBoard(board);

            int bestRank = maximizing ? minRank : maxRank;
            for (int i = moveCount - 1; i >= 0; i--)
            {
                int encodedMove = search.Moves[depth][i];
                int x = encodedMove / board.Height;
                int y = encodedMove % board.Height;
                int rank;

                board.SetValue(x, y, mark);
                try
                {
                    rank = Search(
                        board,
                        search,
                        depth + 1,
                        !maximizing,
                        alpha,
                        beta,
                        x,
                        y,
                        mark);
                }
                finally
                {
                    board.SetValue(x, y, 0);
                }

                if (maximizing)
                {
                    if (rank > bestRank)
                        bestRank = rank;
                    if (bestRank > alpha)
                        alpha = bestRank;
                }
                else
                {
                    if (rank < bestRank)
                        bestRank = rank;
                    if (bestRank < beta)
                        beta = bestRank;
                }

                if (beta <= alpha)
                    break;
            }

            return bestRank;
        }

        private int EvaluateLeaf(GameBoard board, bool maximizing, int depth)
        {
            int markToMove = maximizing ? ComputerGame.COMPUTER_MARK : ComputerGame.PLAYER_MARK;
            int opponentMark = Invert(markToMove);

            if (CountWinningMoves(board, markToMove, 1) > 0)
                return markToMove == ComputerGame.COMPUTER_MARK ? maxRank - depth - 1 : minRank + depth + 1;

            if (CountWinningMoves(board, opponentMark, 2) > 1)
                return opponentMark == ComputerGame.COMPUTER_MARK ? maxRank - depth - 2 : minRank + depth + 2;

            return EvaluateBoard(board);
        }

        private int GenerateMoves(GameBoard board, SearchBuffers search, int depth, int mark)
        {
            int[] moves = search.Moves[depth];
            int[] scores = search.Scores[depth];
            int count = 0;

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Value(x, y) != 0 || !IsNearOccupiedCell(board, x, y))
                        continue;

                    scores[count] = GetMoveOrderScore(board, x, y, mark);
                    moves[count] = (x * board.Height) + y;
                    count++;
                }
            }

            Array.Sort(scores, moves, 0, count);
            return count;
        }

        private int GetMoveOrderScore(GameBoard board, int x, int y, int mark)
        {
            int score = GetPlacementPotential(board, x, y, mark) * 4;
            int opponentMark = Invert(mark);
            score += GetPlacementPotential(board, x, y, opponentMark) * 5;

            board.SetValue(x, y, mark);
            bool wins = IsWinningMove(board, x, y, mark);
            board.SetValue(x, y, 0);
            if (wins)
                score += 1000000;

            board.SetValue(x, y, opponentMark);
            bool blocksWin = IsWinningMove(board, x, y, opponentMark);
            board.SetValue(x, y, 0);
            if (blocksWin)
                score += 500000;

            score -= Math.Abs((board.Width / 2) - x) + Math.Abs((board.Height / 2) - y);
            return score;
        }

        private int GetPlacementPotential(GameBoard board, int x, int y, int mark)
        {
            int score = 0;
            for (int direction = 0; direction < DirectionX.Length; direction++)
            {
                int dx = DirectionX[direction];
                int dy = DirectionY[direction];
                int length = 1
                    + CountConsecutive(board, x, y, dx, dy, mark)
                    + CountConsecutive(board, x, y, -dx, -dy, mark);
                int openEnds = IsOpenEnd(board, x, y, dx, dy, mark) ? 1 : 0;
                if (IsOpenEnd(board, x, y, -dx, -dy, mark))
                    openEnds++;

                score += length * length * (openEnds + 1);
            }
            return score;
        }

        private int CountWinningMoves(GameBoard board, int mark, int stopAfter)
        {
            int wins = 0;

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Value(x, y) != 0 || !IsNearOccupiedCell(board, x, y))
                        continue;

                    board.SetValue(x, y, mark);
                    bool winsHere = IsWinningMove(board, x, y, mark);
                    board.SetValue(x, y, 0);
                    if (winsHere && ++wins == stopAfter)
                        return wins;
                }
            }

            return wins;
        }

        private static bool IsWinningMove(GameBoard board, int x, int y, int mark)
        {
            for (int direction = 0; direction < DirectionX.Length; direction++)
            {
                int dx = DirectionX[direction];
                int dy = DirectionY[direction];
                int length = 1
                    + CountConsecutive(board, x, y, dx, dy, mark)
                    + CountConsecutive(board, x, y, -dx, -dy, mark);
                if (length >= GameBoard.WIN_LENGTH)
                    return true;
            }
            return false;
        }

        private static int CountConsecutive(GameBoard board, int x, int y, int dx, int dy, int mark)
        {
            int count = 0;
            x += dx;
            y += dy;
            while (board.Value(x, y) == mark)
            {
                count++;
                x += dx;
                y += dy;
            }
            return count;
        }

        private static bool IsOpenEnd(GameBoard board, int x, int y, int dx, int dy, int mark)
        {
            x += dx;
            y += dy;
            while (board.Value(x, y) == mark)
            {
                x += dx;
                y += dy;
            }
            return board.Value(x, y) == 0;
        }

        private static int EvaluateBoard(GameBoard board)
        {
            int rank = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x <= board.Width - GameBoard.WIN_LENGTH; x++)
                    rank += EvaluateWindow(board, x, y, 1, 0);
            }

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y <= board.Height - GameBoard.WIN_LENGTH; y++)
                    rank += EvaluateWindow(board, x, y, 0, 1);
            }

            for (int x = 0; x <= board.Width - GameBoard.WIN_LENGTH; x++)
            {
                for (int y = 0; y <= board.Height - GameBoard.WIN_LENGTH; y++)
                    rank += EvaluateWindow(board, x, y, 1, 1);
            }

            for (int x = 0; x <= board.Width - GameBoard.WIN_LENGTH; x++)
            {
                for (int y = GameBoard.WIN_LENGTH - 1; y < board.Height; y++)
                    rank += EvaluateWindow(board, x, y, 1, -1);
            }

            return Math.Max(-MaxHeuristicRank, Math.Min(MaxHeuristicRank, rank));
        }

        private static int EvaluateWindow(GameBoard board, int startX, int startY, int dx, int dy)
        {
            int computerCount = 0;
            int playerCount = 0;
            for (int offset = 0; offset < GameBoard.WIN_LENGTH; offset++)
            {
                int value = board.Value(startX + (offset * dx), startY + (offset * dy));
                if (value == ComputerGame.COMPUTER_MARK)
                    computerCount++;
                else if (value == ComputerGame.PLAYER_MARK)
                    playerCount++;
            }

            if (computerCount > 0 && playerCount > 0)
                return 0;
            if (computerCount > 0)
                return ComputerWeights[computerCount];
            if (playerCount > 0)
                return -PlayerWeights[playerCount];
            return 0;
        }

        public void EvaluateNodeRank(MinimaxNode current)
        {
            if (current == null)
                throw new ArgumentNullException("current");
            if (current.Board == null)
                throw new ArgumentException("The node must have a board.", "current");

            if (current.Board.Winner == ComputerGame.COMPUTER_MARK
                || IsNodeWinningMove(current, ComputerGame.COMPUTER_MARK))
            {
                current.Rank = maxRank;
            }
            else if (current.Board.Winner == ComputerGame.PLAYER_MARK
                || IsNodeWinningMove(current, ComputerGame.PLAYER_MARK))
            {
                current.Rank = minRank;
            }
            else if (current.Depth == maxDepth)
            {
                current.Rank = EvaluateBoard(current.Board);
            }
        }

        private static bool IsNodeWinningMove(MinimaxNode node, int mark)
        {
            return node.Move != null
                && node.Board.Value(node.Move.X, node.Move.Y) == mark
                && IsWinningMove(node.Board, node.Move.X, node.Move.Y, mark);
        }

        private static int Invert(int mark)
        {
            return mark == ComputerGame.COMPUTER_MARK
                ? ComputerGame.PLAYER_MARK
                : ComputerGame.COMPUTER_MARK;
        }

        private static bool HasOccupiedCells(GameBoard board)
        {
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Value(x, y) != 0)
                        return true;
                }
            }

            return false;
        }

        private static bool IsNearOccupiedCell(GameBoard board, int x, int y)
        {
            int startX = Math.Max(0, x - CandidateRadius);
            int endX = Math.Min(board.Width - 1, x + CandidateRadius);
            int startY = Math.Max(0, y - CandidateRadius);
            int endY = Math.Min(board.Height - 1, y + CandidateRadius);

            for (int nearbyX = startX; nearbyX <= endX; nearbyX++)
            {
                for (int nearbyY = startY; nearbyY <= endY; nearbyY++)
                {
                    if (board.Value(nearbyX, nearbyY) != 0)
                        return true;
                }
            }

            return false;
        }

        private sealed class SearchBuffers
        {
            public readonly int[][] Moves;
            public readonly int[][] Scores;

            public SearchBuffers(int capacity)
            {
                Moves = new int[maxDepth][];
                Scores = new int[maxDepth][];
                for (int depth = 0; depth < maxDepth; depth++)
                {
                    Moves[depth] = new int[capacity];
                    Scores[depth] = new int[capacity];
                }
            }
        }
    }
}
