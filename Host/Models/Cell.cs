using System;
using System.Collections.Generic;
using System.Linq;

namespace Host.Models
{
    public class Cell : IComparable<Cell>
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Value { get; set; }
        public Cell(int x, int y)
            :this(x, y ,0)
        {
        }
        public Cell(int x, int y, int value)
        {
            this.X = x;
            this.Y = y;
            this.Value = value;
        }
        public override bool Equals(object obj)
        {
            var trg = (Cell)obj;
            if (trg == null) return false;
            return X == trg.X && Y == trg.Y;
        }
        public int CompareTo(Cell other)
        {
            return X == other.X? Y.CompareTo(other.Y) : X.CompareTo(other.X);
        }
        public override string ToString()
        {
            return "(" + X.ToString() + ":" + Y.ToString() + ")";
        }
    }
}