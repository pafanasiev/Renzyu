using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Host.Models
{
    class ComputerPlayerQueue : PlayerQueue
    {
        protected override Game GetGameSpecific()
        {
            var readyGames = from r in GameRequests
                             where r.IsComputerGame
                             select new ComputerGame(r.Connection);
            return readyGames.FirstOrDefault();
        }
    }
}
