using System;
using System.Collections.Generic;
using System.Linq;

namespace Host.Models
{
    public class HumanGame : Game
    {
        public string PlayerHost { get; private set; }
        public string PlayerGuest { get; private set; }

        public HumanGame(string connection1, string connection2)
            : base()
        {
            if (connection1 == connection2) throw new InvalidOperationException("cannot create a game with only 1 connection");
            this.PlayerHost = connection1;
            this.PlayerGuest = connection2;
        }

        public override MoveResult MakeMove(string connectionId, int x, int y)
        {
            int mark;
            if (PlayerHost == connectionId)
            {
                mark = 1;
            }
            else if (PlayerGuest == connectionId)
            {
                mark = 2;
            }
            else
                throw new ApplicationException("wrong user");
            Row winRow = Board.Move(x, y, mark);
            return new MoveResult
            {
                WinRow = winRow,
                Mark = mark,
                X = x,
                Y = y
            };
        }

        public override Dictionary<string, int> GetPlayersWithMarks()
        {
            return new Dictionary<string, int>() {
                {PlayerHost, 1},
                {PlayerGuest, 2}
            };
        }
    }
}