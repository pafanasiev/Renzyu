using System;
using System.Collections.Generic;
using System.Linq;

namespace Host.Models
{
    public class Row
    {
        public List<Cell> Cells {get;set;}
        public Row(List<Cell> cells)
        {
            this.Cells = cells;
            //cells.Sort((a, b) => { return a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X); });
            cells.Sort();
            //if (!IsVertical() && !IsHorizontal() && !IsForwardDiagonal() && !IsBackwardDiagonal())
            //    throw new ArgumentException("input array is not a row!");
            
        }
        public Row GetWinRow()
        {
            Row result = new Row(new List<Cell>());
            int currentColor = 0;
            for (int i = 0; i < Cells.Count; i++)
            {
                if (Cells[i].Value == 0)
                {
                    result.Cells.Clear();
                    currentColor = 0;
                    continue;
                }
                if (Cells[i].Value == currentColor)
                {
                    result.Cells.Add(Cells[i]);
                    if (result.Cells.Count >= GameBoard.WIN_LENGTH) 
                        break;
                }
                else
                {
                    currentColor = Cells[i].Value;
                    result.Cells.Clear();
                    result.Cells.Add(Cells[i]);
                }
            }
            return result.Cells.Count >= GameBoard.WIN_LENGTH ? result : null;
        }

        public override bool Equals(object obj)
        {
            var target = (Row)obj;
            if (target == null) return false;
            if (target.Cells.Count != this.Cells.Count()) return false;
            for (int i = 0; i < this.Cells.Count; i++)
			{
                if (!target.Cells[i].Equals(this.Cells[i]))
                    return false;
            }
            return true;
        }
        public bool IsHorizontal()
        {
            var ordinate = Cells[0].Y;
            return Cells.All(p => p.Y == ordinate);
        }
        public bool IsVertical()
        {
            var abscis = Cells[0].X;
            return Cells.All(p => p.X == abscis);
        }
        public bool IsForwardDiagonal()
        {
            for (int i = 1; i < Cells.Count; i++)
            {
                if (Cells[i].X != Cells[i - 1].X + 1 || Cells[i].Y != Cells[i - 1].Y + 1)
                    return false;
            }
            return true;
        }
        public bool IsBackwardDiagonal()
        {
            for (int i = 1; i < Cells.Count; i++)
            {
                if (Cells[i].X != Cells[i - 1].X + 1 || Cells[i].Y != Cells[i - 1].Y - 1)
                    return false;
            }
            return true;
        }
    }
}