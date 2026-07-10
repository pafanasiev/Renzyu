using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Host.Models
{
    public abstract class Game
    {
        private static ConcurrentDictionary<Guid, Game> _activeGames = new ConcurrentDictionary<Guid, Game>();

        public GameBoard Board { get; set; }
        public Guid GameId { get; private set; }

        public Game()
        {
            GameId = Guid.NewGuid();
            this.Board = new GameBoard();
            _activeGames.AddOrUpdate(GameId, this, (guid, game) => { return game; });
        }

        public abstract MoveResult MakeMove(string connectionId, int x, int y);
        public abstract Dictionary<string, int> GetPlayersWithMarks();

        public static IEnumerable<Game> GetActiveGames()
        {
            return _activeGames.Values;
        }

        public static Game GetById(Guid gameId)
        {
            return _activeGames[gameId];
        }

        public static IEnumerable<Game> GetByConnectionId(string connectionId)
        {
            return from record in _activeGames
                   where record.Value.GetPlayersWithMarks().Keys.Contains(connectionId)
                   select record.Value;
        }

        public void End()
        {
            Game game;
            _activeGames.TryRemove(this.GameId, out game);
        }
    }
}