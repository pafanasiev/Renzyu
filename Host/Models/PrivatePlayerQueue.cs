using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Host.Models
{
    public class PrivatePlayerQueue : PlayerQueue
    {
        protected override Game GetGameSpecific()
        {
            //find all requests that have the same token and create a game object from them.
            //but exclude pairs of the same game requests 
            var readyGames = from r1 in GameRequests
                             join r2 in GameRequests on r1.Token equals r2.Token
                             where r1.Connection != r2.Connection
                             select new HumanGame(r1.Connection, r2.Connection);

            var result = readyGames.FirstOrDefault();
            return result;
        }
    }
}