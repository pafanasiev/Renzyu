using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Host.Models
{
    public class GameBoard
    {
        public const int WIN_LENGTH = 5;
        public const int SIZE = 19;
        private int _length;
        private int _height;
        private Cell _lastMove;
        private int[,] _board { get; set; }

        public int Winner { get; set; }
        public Cell LastMove { get { return _lastMove; } set {_lastMove = value;} }

        public GameBoard()
            :this(new int[19, 19])
        {
        }
        public GameBoard(int[,] board)
        {
            this._board = board;   
            _length = _board.GetLength(0);
            _height = _board.GetLength(1);
        }
        public Row Move(int x, int y, int value)
        {
            if (x > _length || x < 0 || y > _height || y < 0) throw new ArgumentOutOfRangeException("move is out of range of board");
            if (Value(x, y) > 0) throw new ArgumentException("this point is already set!");
            var targetCell = new Cell(x, y, value);
            if (_lastMove != null && _lastMove.Value == value) throw new ArgumentException("this is not your turn!");
            _board[x, y] = value;
            var winrow = GetHorizontal(targetCell).GetWinRow();
            if (winrow == null)
                winrow = GetVertical(targetCell).GetWinRow();
            if (winrow == null)
                winrow = GetForwardDiagonal(targetCell).GetWinRow();
            if (winrow == null)
                winrow = GetBackwardDiagonal(targetCell).GetWinRow();
            _lastMove = targetCell;
            if (winrow != null) 
                Winner = value;
            return winrow;
        }
        
        public List<Cell> GetAvailableCells()
        {
            //consider only inner rectangle that contains already made moves
            int minI = 1000,
                maxI = 0,
                minJ = 1000,
                maxJ = 0;

            for (int i = 0; i < _length; i++)
            {
                for (int j = 0; j < _height; j++)
                {
                    if (Value(i, j) != 0)
                    {
                        if (minI > i) minI = i;// > 0 ? i : 1;
                        if (maxI < i) maxI = i;// < _length - 1 ? i : _length - 1;
                        if (minJ > j) minJ = j;// > 0 ? j : 1;
                        if (maxJ < j) maxJ = j;// < _height -1 ? j : _height-1;
                    }
                }
            }

            //take rectangle that contains all made moves but 1 line broader
            int startI = minI == 0 ? minI : minI - 1,
                endI = maxI == _length - 1 ? maxI : maxI + 1,
                startJ = minJ == 0 ? minJ : minJ - 1,
                endJ = maxJ == _height - 1 ? maxJ : maxJ + 1;

            var result = new List<Cell>();
            for (int i = startI; i <= endI; i++)
            {
                for (int j = startJ; j <= endJ; j++)
                {
                    if (Value(i, j) == 0)
                    {
                        result.Add(new Cell(i, j));
                    }
                }
            }
            return result;
        }
        public GameBoard Copy()
        {
            var arrayCopy = (int[,])_board.Clone();
            return new GameBoard(arrayCopy);
        }

        private Row GetHorizontal(Cell c)
        {
            var cells = new List<Cell>();
            for (int i = 0; i < _length; i++)
            {
                cells.Add(new Cell(i, c.Y, Value(i, c.Y)));
            }
            return new Row(cells);
        }
        private Row GetVertical(Cell c)
        {
            var cells = new List<Cell>();
            for (int i = 0; i < _height; i++)
            {
                cells.Add(new Cell(c.X, i, Value(c.X, i)));
            }
            return new Row(cells);
        }
        private Row GetForwardDiagonal(Cell c)
        {
            var cells = new List<Cell>();
            int i = c.X;
            int j = c.Y;
            while (i >= 0 && j >= 0)
            {
                cells.Add(new Cell(i, j, Value(i, j)));
                i--;
                j--;
            }
            i = c.X + 1;
            j = c.Y + 1;
            while (i < _length && j < _height)
            {
                cells.Add(new Cell(i, j, Value(i, j)));
                i++;
                j++;
            }
            return new Row(cells);
        }
        private Row GetBackwardDiagonal(Cell c)
        {
            var cells = new List<Cell>();
            int i = c.X;
            int j = c.Y;
            while (i >= 0 && j < _height)
            {
                cells.Add(new Cell(i, j, Value(i, j)));
                i--;
                j++;
            }
            i = c.X + 1;
            j = c.Y - 1;
            while (i < _length && j >= 0)
            {
                cells.Add(new Cell(i, j, Value(i, j)));
                i++;
                j--;
            }
            return new Row(cells);
        }
        public bool IsOnBoard(Cell c)
        {
            return c.X < _length && c.Y < _height && c.X >=0 && c.Y >= 0;
        }
        public int Value(int x, int y)
        {
            if (!IsOnBoard(new Cell(x, y)))
                return -1;
            return _board[x, y];
        }
        public void SetValue(int x, int y, int value)
        {
            _board[x, y] = value;
        }
    }
}
