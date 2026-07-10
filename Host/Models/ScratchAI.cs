using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Host.Models
{
    public class ScratchAI : IAI
    {
        public Cell GetBestMove(GameBoard board)
        {
            return null;
        }


        public void EvaluateNodeRank(MinimaxNode current)
        {
            throw new NotImplementedException();
        }
    }
}