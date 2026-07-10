using Host.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Host.Tests
{
    public class LogicTestBase
    {
        protected int[,] GetBoard(int[,] moves)
        {
            var board = new int[19, 19];
            for (int i = 0; i < moves.GetLength(0); i++)
            {
                board[moves[i, 0], moves[i, 1]] = ComputerGame.COMPUTER_MARK;
            }
            return board;
        }
        protected void AddOpponentMoves(int[,] opponentMoves, ref int[,] board)
        {
            for (int i = 0; i < opponentMoves.GetLength(0); i++)
            {
                board[opponentMoves[i, 0], opponentMoves[i, 1]] = ComputerGame.PLAYER_MARK;
            }
        }

        /*
                7|_|_|_|_|_|_|_|_
                6|_|_|_|_|_|_|_|_
                5|_|_|_|_|_|_|_|_
                4|_|_|_|_|_|_|_|_
                3|_|_|_|_|_|_|_|_
                2|_|_|_|_|_|_|_|_
                1|_|_|_|_|_|_|_|_
                0|_|_|_|_|_|_|_|_
                 |0|1|2|3|4|5|6|7
           */
    }
}
