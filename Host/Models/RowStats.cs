using System;
using System.Collections.Generic;
using System.Linq;

namespace Host.Models
{
    public class RowStats
    {
        public int MaxSeries { get; set; }
        public bool IsBroken { get; set; }
        public bool IsCapped { get; set; }

        //merging stats of a single row made in 2 directions
        public static RowStats operator +(RowStats first, RowStats second)
        {
            var result = new RowStats();

            //if both directions have holes - there's at least uncapped2. 
            //There may be more but we can't be sure
            if (first.IsBroken && second.IsBroken)
                result.MaxSeries = 2;
            else if (first.IsCapped && second.IsCapped)
            {
                if (first.MaxSeries + second.MaxSeries >= GameBoard.WIN_LENGTH)
                    result.MaxSeries = GameBoard.WIN_LENGTH;
                return result; //capped on both sides so no value unless it's a win
            }
            else
            {
                result.MaxSeries = first.MaxSeries + second.MaxSeries;
                result.IsCapped = first.IsCapped || second.IsCapped;
                result.IsBroken = first.IsBroken || second.IsBroken;
            }
            return result;
        }

        public override string ToString()
        {
            string broken = IsBroken ? " broken" : "";
            string capped = IsCapped ? " capped" : "";
            return MaxSeries.ToString() + broken + capped; 
        }
    }
}