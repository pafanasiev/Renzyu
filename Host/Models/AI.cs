using System;

namespace Host.Models
{
    public class AI : IAI
    {
        public const int maxDepth = 2;
        public const int maxRank = 100000;
        public const int minRank = -100000;

        private const int MaxSupportedSearchDepth = 6;
        private const int DefaultSearchDepth = 4;
        private const int DefaultMaxSearchNodes = 60000;
        private const int TranspositionTableSize = 8192;
        private const int NoMove = -1;
        private const byte ExactBound = 0;
        private const byte LowerBound = 1;
        private const byte UpperBound = 2;
        private const int MaxHeuristicRank = 50000;
        private const int CandidateRadius = 2;
        private const int PreferredMoveBonus = 2000000;
        private static readonly int[] MoveLimitsByPly = { 32, 16, 12, 8 };
        private static readonly int[] ComputerWeights = { 0, 2, 12, 180, 6000, 0 };
        private static readonly int[] PlayerWeights = { 0, 2, 12, 180, 6500, 0 };
        private static readonly int[] DirectionX = { 1, 0, 1, 1 };
        private static readonly int[] DirectionY = { 0, 1, 1, -1 };
        private readonly object _searchLock = new object();
        private readonly int _searchDepth;
        private readonly int _maxSearchNodes;
        private SearchWorkspace _workspace;

        public AI()
            : this(DefaultSearchDepth, DefaultMaxSearchNodes)
        {
        }

        public AI(int searchDepth, int maxSearchNodes)
        {
            if (searchDepth < 1 || searchDepth > MaxSupportedSearchDepth)
                throw new ArgumentOutOfRangeException("searchDepth");
            if (maxSearchNodes < 1)
                throw new ArgumentOutOfRangeException("maxSearchNodes");

            _searchDepth = searchDepth;
            _maxSearchNodes = maxSearchNodes;
        }

        public Cell GetBestMove(GameBoard board)
        {
            if (board == null)
                throw new ArgumentNullException("board");
            if (board.Width == 0 || board.Height == 0)
                throw new InvalidOperationException("No legal moves are available.");

            lock (_searchLock)
            {
                return GetBestMoveCore(board);
            }
        }

        private Cell GetBestMoveCore(GameBoard board)
        {
            if (!HasOccupiedCells(board))
                return new Cell(board.Width / 2, board.Height / 2);

            if (_workspace == null || !_workspace.Matches(board))
                _workspace = new SearchWorkspace(board.Width, board.Height);

            SearchWorkspace search = _workspace;
            search.StartSearch();
            ulong hash = search.ComputeHash(board);
            int evaluation = EvaluateBoardRaw(board);
            int bestMove = NoMove;

            for (int depth = 1; depth <= _searchDepth; depth++)
            {
                int iterationMove;
                int iterationRank = Search(
                    board,
                    search,
                    depth,
                    0,
                    true,
                    minRank,
                    maxRank,
                    NoMove,
                    NoMove,
                    0,
                    hash,
                    evaluation,
                    out iterationMove);

                if (search.Aborted)
                    break;
                if (iterationMove != NoMove)
                    bestMove = iterationMove;
                if (iterationRank == maxRank - 1)
                    break;
            }

            if (bestMove == NoMove)
                throw new InvalidOperationException("No legal moves are available.");

            return new Cell(bestMove / board.Height, bestMove % board.Height);
        }

        private int Search(
            GameBoard board,
            SearchWorkspace search,
            int remainingDepth,
            int ply,
            bool maximizing,
            int alpha,
            int beta,
            int lastX,
            int lastY,
            int lastMark,
            ulong hash,
            int evaluation,
            out int bestMove)
        {
            bestMove = NoMove;
            search.Nodes++;
            if (search.Nodes > _maxSearchNodes)
            {
                search.Aborted = true;
                return 0;
            }

            if (lastMark != 0 && IsWinningMove(board, lastX, lastY, lastMark))
                return lastMark == ComputerGame.COMPUTER_MARK ? maxRank - ply : minRank + ply;

            if (remainingDepth == 0)
                return ClampHeuristicRank(evaluation);

            int originalAlpha = alpha;
            int originalBeta = beta;
            int mark = maximizing ? ComputerGame.COMPUTER_MARK : ComputerGame.PLAYER_MARK;
            ulong key = hash ^ search.GetSideKey(mark);
            int preferredMove = NoMove;
            TranspositionEntry cached;
            if (search.TryGetEntry(key, out cached))
            {
                preferredMove = cached.BestMove;
                if (cached.Depth >= remainingDepth)
                {
                    if (cached.Bound == ExactBound)
                    {
                        bestMove = cached.BestMove;
                        return cached.Value;
                    }
                    if (cached.Bound == LowerBound && cached.Value > alpha)
                        alpha = cached.Value;
                    else if (cached.Bound == UpperBound && cached.Value < beta)
                        beta = cached.Value;

                    if (alpha >= beta)
                    {
                        bestMove = cached.BestMove;
                        return cached.Value;
                    }
                }
            }

            int moveLimit = MoveLimitsByPly[Math.Min(ply, MoveLimitsByPly.Length - 1)];
            int moveCount = GenerateMoves(board, search, ply, mark, preferredMove, moveLimit);
            if (moveCount == 0)
            {
                int value = ClampHeuristicRank(evaluation);
                search.StoreEntry(key, remainingDepth, value, ExactBound, NoMove);
                return value;
            }

            int bestRank = maximizing ? minRank : maxRank;
            for (int i = moveCount - 1; i >= 0; i--)
            {
                int encodedMove = search.Moves[ply][i];
                int x = encodedMove / board.Height;
                int y = encodedMove % board.Height;
                int rank;
                int affectedEvaluation = EvaluateAffectedWindows(board, x, y);

                board.SetValue(x, y, mark);
                try
                {
                    int childEvaluation = evaluation
                        - affectedEvaluation
                        + EvaluateAffectedWindows(board, x, y);
                    int childMove;
                    rank = Search(
                        board,
                        search,
                        remainingDepth - 1,
                        ply + 1,
                        !maximizing,
                        alpha,
                        beta,
                        x,
                        y,
                        mark,
                        hash ^ search.GetPieceKey(encodedMove, mark),
                        childEvaluation,
                        out childMove);
                }
                finally
                {
                    board.SetValue(x, y, 0);
                }

                if (search.Aborted)
                    return 0;

                if (maximizing)
                {
                    if (rank > bestRank)
                    {
                        bestRank = rank;
                        bestMove = encodedMove;
                    }
                    if (bestRank > alpha)
                        alpha = bestRank;
                }
                else
                {
                    if (rank < bestRank)
                    {
                        bestRank = rank;
                        bestMove = encodedMove;
                    }
                    if (bestRank < beta)
                        beta = bestRank;
                }

                if (beta <= alpha)
                    break;
            }

            byte bound = ExactBound;
            if (bestRank <= originalAlpha)
                bound = UpperBound;
            else if (bestRank >= originalBeta)
                bound = LowerBound;
            search.StoreEntry(key, remainingDepth, bestRank, bound, bestMove);
            return bestRank;
        }

        private int GenerateMoves(
            GameBoard board,
            SearchWorkspace search,
            int ply,
            int mark,
            int preferredMove,
            int moveLimit)
        {
            int[] moves = search.Moves[ply];
            int[] scores = search.Scores[ply];
            int count = 0;
            int forcingLevel = 0;
            int opponentMark = Invert(mark);

            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    if (board.Value(x, y) != 0 || !IsNearOccupiedCell(board, x, y))
                        continue;

                    bool wins = IsWinningMove(board, x, y, mark);
                    bool blocksWin = IsWinningMove(board, x, y, opponentMark);
                    int moveForcingLevel = wins ? 2 : blocksWin ? 1 : 0;
                    if (moveForcingLevel < forcingLevel)
                        continue;
                    if (moveForcingLevel > forcingLevel)
                    {
                        forcingLevel = moveForcingLevel;
                        count = 0;
                    }

                    int encodedMove = (x * board.Height) + y;
                    int score = GetMoveOrderScore(board, x, y, mark, wins, blocksWin);
                    if (encodedMove == preferredMove)
                        score += PreferredMoveBonus;
                    scores[count] = score;
                    moves[count] = encodedMove;
                    count++;
                }
            }

            Array.Sort(scores, moves, 0, count);
            if (count > moveLimit)
            {
                int firstMove = count - moveLimit;
                Array.Copy(scores, firstMove, scores, 0, moveLimit);
                Array.Copy(moves, firstMove, moves, 0, moveLimit);
                count = moveLimit;
            }
            return count;
        }

        private int GetMoveOrderScore(
            GameBoard board,
            int x,
            int y,
            int mark,
            bool wins,
            bool blocksWin)
        {
            int score = GetPlacementPotential(board, x, y, mark) * 4;
            int opponentMark = Invert(mark);
            score += GetPlacementPotential(board, x, y, opponentMark) * 5;

            if (wins)
                score += 1000000;

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
            return ClampHeuristicRank(EvaluateBoardRaw(board));
        }

        private static int EvaluateBoardRaw(GameBoard board)
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

            return rank;
        }

        private static int EvaluateAffectedWindows(GameBoard board, int x, int y)
        {
            int rank = 0;
            for (int direction = 0; direction < DirectionX.Length; direction++)
            {
                int dx = DirectionX[direction];
                int dy = DirectionY[direction];
                for (int offset = 0; offset < GameBoard.WIN_LENGTH; offset++)
                {
                    int startX = x - (offset * dx);
                    int startY = y - (offset * dy);
                    int endX = startX + ((GameBoard.WIN_LENGTH - 1) * dx);
                    int endY = startY + ((GameBoard.WIN_LENGTH - 1) * dy);
                    if (startX < 0 || startX >= board.Width
                        || startY < 0 || startY >= board.Height
                        || endX < 0 || endX >= board.Width
                        || endY < 0 || endY >= board.Height)
                    {
                        continue;
                    }

                    rank += EvaluateWindow(board, startX, startY, dx, dy);
                }
            }
            return rank;
        }

        private static int ClampHeuristicRank(int rank)
        {
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

        private struct TranspositionEntry
        {
            public ulong Key;
            public int Value;
            public int BestMove;
            public int Depth;
            public int Generation;
            public byte Bound;
        }

        private sealed class SearchWorkspace
        {
            public readonly int[][] Moves;
            public readonly int[][] Scores;
            private readonly int _width;
            private readonly int _height;
            private readonly ulong[] _pieceKeys;
            private readonly ulong[] _sideKeys;
            private readonly TranspositionEntry[] _entries;
            private int _generation;

            public int Nodes { get; set; }
            public bool Aborted { get; set; }

            public SearchWorkspace(int width, int height)
            {
                _width = width;
                _height = height;
                int capacity = checked(width * height);
                Moves = new int[MaxSupportedSearchDepth][];
                Scores = new int[MaxSupportedSearchDepth][];
                for (int depth = 0; depth < MaxSupportedSearchDepth; depth++)
                {
                    Moves[depth] = new int[capacity];
                    Scores[depth] = new int[capacity];
                }

                _pieceKeys = new ulong[capacity * 2];
                _sideKeys = new ulong[2];
                ulong randomState = 0xD1B54A32D192ED03UL
                    ^ ((ulong)(uint)width << 32)
                    ^ (uint)height;
                for (int i = 0; i < _pieceKeys.Length; i++)
                    _pieceKeys[i] = NextRandom(ref randomState);
                for (int i = 0; i < _sideKeys.Length; i++)
                    _sideKeys[i] = NextRandom(ref randomState);

                _entries = new TranspositionEntry[TranspositionTableSize];
            }

            public bool Matches(GameBoard board)
            {
                return board.Width == _width && board.Height == _height;
            }

            public void StartSearch()
            {
                Nodes = 0;
                Aborted = false;
                if (_generation == int.MaxValue)
                {
                    Array.Clear(_entries, 0, _entries.Length);
                    _generation = 1;
                }
                else
                {
                    _generation++;
                }
            }

            public ulong ComputeHash(GameBoard board)
            {
                ulong hash = 0;
                for (int x = 0; x < board.Width; x++)
                {
                    for (int y = 0; y < board.Height; y++)
                    {
                        int mark = board.Value(x, y);
                        if (mark == ComputerGame.PLAYER_MARK || mark == ComputerGame.COMPUTER_MARK)
                            hash ^= GetPieceKey((x * board.Height) + y, mark);
                    }
                }
                return hash;
            }

            public ulong GetPieceKey(int encodedMove, int mark)
            {
                int markIndex = mark == ComputerGame.COMPUTER_MARK ? 1 : 0;
                return _pieceKeys[(encodedMove * 2) + markIndex];
            }

            public ulong GetSideKey(int mark)
            {
                return _sideKeys[mark == ComputerGame.COMPUTER_MARK ? 1 : 0];
            }

            public bool TryGetEntry(ulong key, out TranspositionEntry entry)
            {
                entry = _entries[(int)(key & (TranspositionTableSize - 1))];
                return entry.Generation == _generation && entry.Key == key;
            }

            public void StoreEntry(ulong key, int depth, int value, byte bound, int bestMove)
            {
                int index = (int)(key & (TranspositionTableSize - 1));
                TranspositionEntry existing = _entries[index];
                if (existing.Generation == _generation && existing.Depth > depth)
                    return;

                _entries[index] = new TranspositionEntry
                {
                    Key = key,
                    Value = value,
                    BestMove = bestMove,
                    Depth = depth,
                    Generation = _generation,
                    Bound = bound,
                };
            }

            private static ulong NextRandom(ref ulong state)
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong value = state;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }
    }
}
