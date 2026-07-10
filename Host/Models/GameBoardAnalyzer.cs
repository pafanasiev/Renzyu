using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Host.Models
{
    public class GameBoardAnalyzer
    {
        private GameBoard board;
        public GameBoardAnalyzer(GameBoard board)
        {
            this.board = board;
        }

        public List<RowStats> GetMoveStats(int mark)
        {
            var result = new List<RowStats>();
            for (int i = board.LastMove.Y - 4; i < board.LastMove.Y + 4; i++)
                result.Add(GetStatsInDirection(0, i, 1, 0, mark));
            for (int i = board.LastMove.X - 4; i < board.LastMove.X + 4; i++)
                result.Add(GetStatsInDirection(i, 0, 0, 1, mark));
            for (int i = board.LastMove.X - 4; i < board.LastMove.X + 4; i++)
            {
                result.Add(GetStatsInDirection(GetStartForFWDiagonal(i, board.LastMove.Y), 1, 1, mark));
                result.Add(GetStatsInDirection(GetStartForBWDiagonal(i, board.LastMove.Y), 1, -1, mark));
            }
            return result;
        }

        private Cell GetStartForFWDiagonal(int x1, int y1)
        {
            var c = y1 - x1;
            int x = Math.Max(-c, 0);
            int y = c + x;
            return new Cell(x, y);
        }

        //gets the top left point on the board that lies on the same diagonal as lastmove
        private Cell GetStartForBWDiagonal(int x1, int y1)
        {
            var c = x1 + y1;
            int x = Math.Max(c - GameBoard.SIZE, 0);
            int y = c - x;
            return new Cell(x, y);
        }
        public RowStats GetStatsInDirection(int startX, int startY, int dx, int dy, int playerMark)
        {
            return GetStatsInDirection(new Cell(startX, startY), dx, dy, playerMark);
        }

        public RowStats GetStatsInDirection(Cell startPoint, int dx, int dy, int playerMark)
        {
            var result = new RowStats();
            RowStats best = new RowStats();
            if (!board.IsOnBoard(startPoint))
            {
                result.IsCapped = true;
                return result;
            }
            startPoint.Value = board.Value(startPoint.X, startPoint.Y);
            var currentPoint = startPoint;
            Cell previousPoint = null;
            
            while (board.IsOnBoard(currentPoint) && result.MaxSeries <= GameBoard.WIN_LENGTH)
            {
                if (currentPoint.Value == playerMark)
                    result.MaxSeries++;
                else if (currentPoint.Value == 0)
                {
                    if (result.IsBroken)
                    {// if there was one breach already we just end. E.g: "x xx ".
                        if (previousPoint != null && previousPoint.Value == 0)
                            result.IsBroken = false; //if this is 2nd consequent breach - it's just end of series, no breach. E.g. "xxx  ".
                        if (result.MaxSeries > best.MaxSeries)
                        {
                            best = result;
                        }
                        result = new RowStats();
                    }
                    else
                    {
                        result.IsBroken = true;
                    }
                }
                else //opponent mark
                {
                    if (previousPoint != null && previousPoint.Value == 0) //it's not a broken and not a capped serie. E.g: "xxx 0"
                    {
                        result.IsBroken = false; //set the flag back to false, since it was not a breach but the end of series.
                    }
                    else
                    {
                        result.IsCapped = true;
                    }
                    if (result.MaxSeries > best.MaxSeries)
                    {
                        best = result;
                    }
                    result = new RowStats();
                }
                previousPoint = currentPoint;
                currentPoint = GetNextCell(currentPoint, dx, dy);
            }
            return result.MaxSeries > best.MaxSeries ? result : best;
        }

        private Cell GetNextCell(Cell c, int dx, int dy)
        {
            var nextCell = new Cell(c.X + dx, c.Y + dy);
                nextCell.Value = board.Value(nextCell.X, nextCell.Y);
            return nextCell;
        }
    }
}