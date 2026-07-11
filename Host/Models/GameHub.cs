using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Host.Models
{
    [HubName("game")]
    public class GameHub : Hub
    {
        public void SendMove(int x, int y)
        {
            Game game = GetCurrentGame();
            MoveResult result = game.MakeMove(Context.ConnectionId, x, y);
            ProcessMoveResult(result, game);
        }

        public void Enqueue(GameRequest request)
        {
            PlayerQueue queue = PlayerQueue.Add(request);
            var game = queue.GetGame();

            //if we were able to find an opponent for this game request
            if (game != null)
            {
                CreateSignalRGroupForGame(game);
                SendJoinNotificationsToGroup(game);
            }
        }

        private void SendJoinNotificationsToGroup(Game game)
        {
            //item.Key - connection
            //item.Value - mark
            foreach (var item in game.GetPlayersWithMarks())
            {
                Clients.Client(item.Key).receiveJoin(game.GameId.ToString(), item.Value);
            }
        }

        private void CreateSignalRGroupForGame(Game game)
        {
            foreach (var connection in game.GetPlayersWithMarks().Keys)
            {
                Groups.Add(connection, game.GameId.ToString());
            }
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            //remove the connection from queue if it's still waiting for opponent
            PlayerQueue.Dequeue(Context.ConnectionId);

            //notify all opponents in all games about disconnect
            var games = Game.GetByConnectionId(Context.ConnectionId);
            foreach (var game in games)
            {
                Clients.Group(game.GameId.ToString()).receiveDisconnect();

                //remove the game and free memory
                game.End();
            }
            return base.OnDisconnected(stopCalled);
        }

        private void ProcessMoveResult(MoveResult result, Game game)
        {
            if (result == null) return;

            Clients.Group(game.GameId.ToString()).receiveMove(result.X, result.Y, result.Mark);
            if (result.WinRow != null)
            {
                Clients.Group(game.GameId.ToString()).receiveWin(result.WinRow.Cells, result.Mark);

                //remove the game
                game.End();
            }
            //recursively process next move if the game could return it already
            ProcessMoveResult(result.OpponentMove, game);
        }

        private Game GetCurrentGame()
        {
            Guid gameId = Guid.Parse(Clients.Caller.gameId);
            return Game.GetById(gameId);
        }
    }
}