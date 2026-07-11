using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Host.Models
{
    public abstract class PlayerQueue
    {
        public static object lockObject = new object();
        public static List<GameRequest> GameRequests = new List<GameRequest>();

        protected void Enqueue(GameRequest request)
        {
            lock (lockObject)
            {
                var existingRequest = GameRequests.SingleOrDefault(r => r.Connection == request.Connection);
                if (existingRequest == null)
                    GameRequests.Add(request);
                else
                {
                    existingRequest.IsComputerGame = request.IsComputerGame;
                    existingRequest.Token = request.Token;
                }
            }
        }

        public static void Dequeue(params string[] connections)
        {
            lock (lockObject)
            {
                GameRequests.RemoveAll(r => connections.Contains(r.Connection));
            }
        }

        protected abstract Game GetGameSpecific();

        public Game GetGame()
        {
            lock (lockObject)
            {
                var game = GetGameSpecific();
                if (game != null)
                    Dequeue(game.GetPlayersWithMarks().Keys.ToArray());
                return game;
            }
        }

        public static PlayerQueue Add(GameRequest request)
        {
            if (request.Connection == null) throw new ArgumentNullException("connection cannot be null");

            PlayerQueue queue;
            if (request.IsComputerGame)
                queue = new ComputerPlayerQueue();
            else if (request.IsPublicGame)
                queue = new PublicGamePlayerQueue();
            else
                queue = new PrivatePlayerQueue();
            
            queue.Enqueue(request);
            return queue;
        }
    }
}