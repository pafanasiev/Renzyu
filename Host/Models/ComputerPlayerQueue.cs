using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Host.Models
{
    class ComputerPlayerQueue : PlayerQueue
    {
        private readonly IAiModelCatalog aiModelCatalog;
        private readonly string connection;

        public ComputerPlayerQueue(string connection, IAiModelCatalog aiModelCatalog)
        {
            this.connection = connection
                ?? throw new ArgumentNullException(nameof(connection));
            this.aiModelCatalog = aiModelCatalog
                ?? throw new ArgumentNullException(nameof(aiModelCatalog));
        }

        protected override Game GetGameSpecific()
        {
            var readyGames = from r in GameRequests
                             where r.IsComputerGame && r.Connection == connection
                             select new ComputerGame(
                                 r.Connection,
                                 aiModelCatalog.CreateAI(r.AiModelId),
                                 null);
            return readyGames.FirstOrDefault();
        }
    }
}
