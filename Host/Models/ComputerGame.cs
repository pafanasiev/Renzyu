using System;
using System.Collections.Generic;
using System.Linq;

namespace Host.Models
{
    public class ComputerGame : Game
    {
        public const int PLAYER_MARK = 1;
        public const int COMPUTER_MARK = 2;
        private string player;
        private AI ai;

        public ComputerGame(string connection)
        {
            this.player = connection;
            ai = new AI();
        }

        public override Dictionary<string, int> GetPlayersWithMarks()
        {
            return new Dictionary<string, int> { { player, PLAYER_MARK } };
        }

        public override MoveResult MakeMove(string connectionId, int x, int y)
        {
            if (connectionId != player) throw new InvalidOperationException("wrong player connection");
            Row playerWinRow = Board.Move(x, y, PLAYER_MARK);
            var result = new MoveResult
            {
                Mark = PLAYER_MARK,
                X = x,
                Y = y,
                WinRow = playerWinRow
            };
            if (playerWinRow == null)
                result.OpponentMove = MakeComputerMove();
            return result;
        }

        private MoveResult MakeComputerMove()
        {
            Cell move = ai.GetBestMove(Board);
            Row winRow = Board.Move(move.X, move.Y, COMPUTER_MARK);
            var result = new MoveResult
            {
                Mark = COMPUTER_MARK,
                X = move.X,
                Y = move.Y,
                WinRow = winRow
            };
            return result;
        }

       
    }
}