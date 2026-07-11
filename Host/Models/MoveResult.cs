using System;
using System.Collections.Generic;
using System.Linq;

namespace Host.Models
{
    public class MoveResult
    {
        public int Mark { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public Row WinRow { get; set; }

        public MoveResult OpponentMove { get; set; }
    }
}