using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Host.Models
{
    public class PublicGamePlayerQueue : PlayerQueue
    {
        protected override Game GetGameSpecific()
        {
            var reqs = GameRequests.TakeWhile((request, index) => request.IsPublicGame && index < 2).ToList();
            if (reqs.Count > 1)
            {
                return new HumanGame(reqs[0].Connection, reqs[1].Connection);
            }
            return null;
        }
    }
}