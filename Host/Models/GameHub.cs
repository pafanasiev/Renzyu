using Microsoft.AspNetCore.SignalR;

namespace Host.Models
{
    public interface IGameClient
    {
        Task ReceiveDisconnect();
        Task ReceiveJoin(string gameId, int mark);
        Task ReceiveMove(int x, int y, int mark);
        Task ReceiveWin(IReadOnlyCollection<Cell> cells, int mark);
    }

    public sealed class GameHub : Hub<IGameClient>
    {
        public async Task SendMove(int x, int y)
        {
            var game = GetCurrentGame();
            var result = game.MakeMove(Context.ConnectionId, x, y);

            await ProcessMoveResultAsync(result, game);
        }

        public async Task Enqueue(GameRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            request.Connection = Context.ConnectionId;
            var queue = PlayerQueue.Add(request);
            var game = queue.GetGame();
            if (game == null)
            {
                return;
            }

            await CreateSignalRGroupForGameAsync(game);
            await SendJoinNotificationsToGroupAsync(game);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            PlayerQueue.Dequeue(Context.ConnectionId);

            var games = Game.GetByConnectionId(Context.ConnectionId).ToArray();
            foreach (var game in games)
            {
                await Clients.Group(game.GameId.ToString()).ReceiveDisconnect();
                game.End();
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task SendJoinNotificationsToGroupAsync(Game game)
        {
            var notifications = game.GetPlayersWithMarks().Select(player =>
                Clients.Client(player.Key).ReceiveJoin(game.GameId.ToString(), player.Value));

            await Task.WhenAll(notifications);
        }

        private async Task CreateSignalRGroupForGameAsync(Game game)
        {
            var groupName = game.GameId.ToString();
            var additions = game.GetPlayersWithMarks().Keys.Select(connectionId =>
                Groups.AddToGroupAsync(connectionId, groupName));

            await Task.WhenAll(additions);
        }

        private async Task ProcessMoveResultAsync(MoveResult result, Game game)
        {
            while (result != null)
            {
                await Clients.Group(game.GameId.ToString())
                    .ReceiveMove(result.X, result.Y, result.Mark);

                if (result.WinRow != null)
                {
                    await Clients.Group(game.GameId.ToString())
                        .ReceiveWin(result.WinRow.Cells, result.Mark);
                    game.End();
                    return;
                }

                result = result.OpponentMove;
            }
        }

        private Game GetCurrentGame()
        {
            var games = Game.GetByConnectionId(Context.ConnectionId).Take(2).ToArray();
            if (games.Length != 1)
            {
                throw new HubException("The connection is not associated with exactly one active game.");
            }

            return games[0];
        }
    }
}
