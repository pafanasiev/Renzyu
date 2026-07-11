using System;
using System.Collections.Generic;
using System.Linq;

namespace Host.Models
{
    public class Player
    {
        private List<string> _connections;

        public string UserName { get; set; }
        public bool IsHost { get; set; }
        public IEnumerable<string> Connections { get {return _connections;}}
        public string Color { get; set; }

        public Player(string userName, bool isHost)
        {
            this.UserName = userName;
            this.IsHost = isHost;
            _connections = new List<string>();
        }

        public void AddConnection(string connectionId)
        {
            if (!Connections.Contains(connectionId))
                _connections.Add(connectionId);
        }
    }
}