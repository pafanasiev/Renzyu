using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Host.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Host.Tests
{
    [TestClass]
    public class AITests : LogicTestBase
    {
        /*
             7|_|_|_|_|_|_|_|_
             6|_|_|_|_|_|_|_|_
             5|_|_|x|x|_|x|x|_
             4|_|_|_|_|_|_|_|_
             3|_|_|_|_|_|_|_|_
             2|_|_|_|_|_|_|_|_
             1|_|_|_|_|_|_|_|_
             0|_|_|_|_|_|_|_|_
              |0|1|2|3|4|5|6|7
        */
        [TestMethod]
        public void GetBestMove_OwnOpen4OpponentNone_SetsWinningPoint()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2,5},
                {3,5},
                {5,5},
                {6,5}
            });
            //assert
            AssertMove(board, 4, 5);
        }

        /*
             7|_|_|_|_|_|_|_|_
             6|x|_|_|_|_|_|_|_
             5|x|x|0|0|0|0|_|_
             4|x|_|_|_|_|_|_|_
             3|_|_|_|_|_|_|_|_
             2|_|_|_|_|_|_|_|_
             1|_|_|_|_|_|_|_|_
             0|_|_|_|_|_|_|_|_
              |0|1|2|3|4|5|6|7
        */
        [TestMethod]
        public void GetBestMove_OwnNoneOpponentClosed4_ClosesOpponent4()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {1,5},
                {0, 4},
                {0, 5},
                {0, 6},
            });
            AddOpponentMoves(new int[,]
            {
                 {2,5},
                {3,5},
                {4,5},
                {5,5}
            }, ref board);
            //act
            AssertMove(board, 6, 5);
        }

        /*
          7|_|_|_|_|_|_|_|_
          6|_|_|_|_|_|_|_|_
          5|_|x|_|_|_|_|_|_
          4|_|x|_|0|0|0|_|_
          3|_|x|_|_|_|_|_|_
          2|_|_|_|_|_|_|_|_
          1|_|_|_|_|_|_|_|_
          0|_|_|_|_|_|_|_|_
           |0|1|2|3|4|5|6|7
     */
        [TestMethod]
        public void GetBestMove_OwnOpen3OpponentOpen3_FinishesOwn()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {1, 3},
                {1, 4},
                {1, 5},
            });
            AddOpponentMoves(new int[,]
            {
                {3,4},
                {4,4},
                {5,4}
            }, ref board);
            //act
            AssertEitherMove(board, 1, 2, 1, 6);
        }

        /*
                  7|_|_|_|_|_|_|_|_
                  6|_|_|_|_|_|_|_|_
                  5|_|x|_|_|_|_|_|_
                  4|_|x|_|0|0|0|_|_
                  3|_|x|_|_|_|_|_|_
                  2|_|0|_|_|_|_|_|_
                  1|_|_|_|_|_|_|_|_
                  0|_|_|_|_|_|_|_|_
                   |0|1|2|3|4|5|6|7
             */
        [TestMethod]
        public void GetBestMove_OwnClosed3OpponentOpen3_ClosesOpponent()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {1, 3},
                {1, 4},
                {1, 5},
            });
            AddOpponentMoves(new int[,]
            {
                {1,2},
                {3,4},
                {4,4},
                {5,4}
            }, ref board);
            //act
            AssertEitherMove(board, 2, 4, 6, 4); 
        }

        /*
                7|_|_|_|_|_|_|_|_
                6|_|_|_|_|_|_|_|_
                5|_|x|_|_|_|_|_|_
                4|_|x|_|0|0|0|0|_
                3|_|x|_|_|_|_|_|_
                2|_|0|x|_|_|_|_|_
                1|_|_|_|_|_|_|_|_
                0|_|_|_|_|_|_|_|_
                 |0|1|2|3|4|5|6|7
           */
        [TestMethod]
        public void GetBestMove_OwnClosed3OpponentOpen4_ClosesOpponent()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {1, 3},
                {1, 4},
                {1, 5},
                {2, 2},
            });
            AddOpponentMoves(new int[,]
            {
                {1,2},
                {3,4},
                {4,4},
                {5,4},
                {6,4},
            }, ref board);
            //act
            AssertEitherMove(board, 2, 4, 7, 4);
        }

        /*
                  7|_|_|_|_|_|_|_|_
                  6|_|_|_|_|_|_|_|_
                  5|_|x|_|_|_|_|_|_
                  4|_|x|_|0|0|_|0|_
                  3|_|x|_|_|_|_|_|_
                  2|_|0|_|_|_|_|_|_
                  1|_|_|_|_|_|_|_|_
                  0|_|_|_|_|_|_|_|_
                   |0|1|2|3|4|5|6|7
             */
        [TestMethod]
        public void GetBestMove_OwnClosed3OpponentDamaged4_ClosesOpponent()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {1, 3},
                {1, 4},
                {1, 5},
            });
            AddOpponentMoves(new int[,]
            {
                {1,2},
                {3,4},
                {4,4},
                {6,4}
            }, ref board);
            //act
            AssertMove(board, 5, 4);
        }

        /*
           7|_|_|_|0|_|_|_|_
           6|_|_|_|x|_|_|_|_
           5|_|_|_|x|_|_|_|_
           4|_|_|_|x|0|0|0|0
           3|_|_|_|x|_|_|_|_
           2|_|_|_|_|_|_|_|_
           1|_|_|_|_|_|_|_|_
           0|_|_|_|_|_|_|_|_
            |0|1|2|3|4|5|6|7
      */
        [TestMethod]
        public void GetBestMove_OwnClosed4Other4_GoesForAWin()
        {
            var board = GetBoard(new int[,] 
            {
                {3,3},
                {3,4},
                {3,5},
                {3,6},
            });
            AddOpponentMoves(new int[,]
            {
                {3, 7},
                {4, 4},
                {5, 4},
                {6, 4},
                {7, 4},
            }, ref board);
            AssertMove(board, 3, 2);
        }

        /*
           7|_|_|_|_|_|_|_|_
           6|_|_|_|_|_|_|_|_
           5|0|_|x|x|_|x|x|_
           4|_|_|_|_|_|_|_|_
           3|_|_|_|_|_|_|_|_
           2|_|_|_|_|_|_|_|_
           1|_|_|_|_|_|_|_|_
           0|_|_|_|_|_|_|_|_
            |0|1|2|3|4|5|6|7
      */
        [TestMethod]
        public void EvaluateNodeRank()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2,5},
                {3,5},
                {5,5},
                {6,5}
            });
            AddOpponentMoves(new int[,]
            {
                {1, 5},
            }, ref board);
            var node = new MaxNode(new Cell(1, 5), new GameBoard(board) { LastMove = new Cell(1, 5, 1) })
            {
                Depth = AI.maxDepth,
            };
            GetAI().EvaluateNodeRank(node);
            
        }

        

        private void AssertMove(int[,] board, int x, int y)
        {
            Cell actualResult = GetAI().GetBestMove(new GameBoard(board));
            if (x != actualResult.X || y != actualResult.Y)
                throw new AssertFailedException(String.Format("expected ({0}:{1}), actual {2}", x, y, actualResult));
        }

        private void AssertEitherMove(int[,] board, int x1, int y1, int x2, int y2)
        {
            Cell actualResult = GetAI().GetBestMove(new GameBoard(board));
            if ((x1 != actualResult.X || y1 != actualResult.Y) && (x2 != actualResult.X || y2 != actualResult.Y))
                throw new AssertFailedException(String.Format("expected ({0}:{1}) or ({2}:{3}), actual {4}", x1, y1, x2, y2, actualResult));
        }

        private IAI GetAI()
        {
            //return new ScratchAI();
            return new AI();
        }
    }
}

