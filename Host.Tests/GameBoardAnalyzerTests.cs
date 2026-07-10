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
    public class GameBoardAnalyzerTests : LogicTestBase
    {
        /*
            2|_|_|S|0|0|_|0|_
            1|_|_|_|_|_|_|_|_
            0|_|_|_|_|_|_|_|_
             |0|1|2|3|4|5|6|7
        */
        [TestMethod]
        public void GetStatsInDirection_Broken5()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2, 2},
                {3, 2},
                {4, 2},
                {6, 2},
            });
            var target = new GameBoardAnalyzer(new GameBoard(board) { LastMove = new Cell(2, 2, 2) });
            RowStats result = target.GetStatsInDirection(new Cell(2, 2, 2), 1, 0, 2);
            Assert.AreEqual(4, result.MaxSeries);
            Assert.IsTrue(result.IsBroken, "expected broken series");
            Assert.IsFalse(result.IsCapped, "expected uncapped series");
        }

        /*
           2|_|S|0|0|_|0|0|_
           1|_|_|_|_|_|_|_|_
           0|_|_|_|_|_|_|_|_
            |0|1|2|3|4|5|6|7
       */
        [TestMethod]
        public void GetStatsInDirection_BrokenCapped4()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2, 2},
                {3, 2},
                {5, 2},
                {6, 2},
            });
            AddOpponentMoves(new int[,]
            {
                {1,2}
            }, ref board);

            var target = new GameBoardAnalyzer(new GameBoard(board) { LastMove = new Cell(1, 2, 1) });
            RowStats result = target.GetStatsInDirection(new Cell(1, 2, 1), 1, 0, 2);
            Assert.AreEqual(4, result.MaxSeries);
            Assert.IsTrue(result.IsBroken, "expected broken series");
            Assert.IsTrue(result.IsCapped, "expected capped series");
        }

        /*
          2|_|_|0|0|S|0|0|_
          1|_|_|_|_|_|_|_|_
          0|_|_|_|_|_|_|_|_
           |0|1|2|3|4|5|6|7
      */
        [TestMethod]
        public void GetStatsInDirection_BrokenCappedInside4()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2, 2},
                {3, 2},
                {5, 2},
                {6, 2},
            });
            AddOpponentMoves(new int[,]
            {
                {4,2}
            }, ref board);

            var target = new GameBoardAnalyzer(new GameBoard(board) { LastMove = new Cell(2, 2, 2) });
            RowStats result = target.GetStatsInDirection(new Cell(2, 2, 2), 1, 0, 2);
            Assert.AreEqual(2, result.MaxSeries);
            Assert.IsFalse(result.IsBroken, "expected unbroken series");
            Assert.IsTrue(result.IsCapped, "expected capped series");
        }

        /*
          2|_|_|S|0|0|0|_|_
          1|_|_|_|_|_|_|_|_
          0|_|_|_|_|_|_|_|_
           |0|1|2|3|4|5|6|7
        */
        [TestMethod]
        public void GetStatsInDirection_UnCapped4()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2, 2},
                {3, 2},
                {4, 2},
                {5, 2},
            });
            var target = new GameBoardAnalyzer(new GameBoard(board));
            RowStats result = target.GetStatsInDirection(new Cell(2, 2, 2), 1, 0, 2);
            Assert.AreEqual(4, result.MaxSeries);
            Assert.IsFalse(result.IsBroken, "expected not broken series");
            Assert.IsFalse(result.IsCapped, "expected uncapped series");
        }

        /*
       2|_|_|S|0|0|0|X|_
       1|_|_|_|_|_|_|_|_
       0|_|_|_|_|_|_|_|_
        |0|1|2|3|4|5|6|7
  */
        [TestMethod]
        public void GetStatsInDirection_Capped4()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2, 2},
                {3, 2},
                {4, 2},
                {5, 2},
            });
            AddOpponentMoves(new int[,]
            {
                {6,2}
            }, ref board);
            var target = new GameBoardAnalyzer(new GameBoard(board));
            RowStats result = target.GetStatsInDirection(new Cell(2, 2, 2), 1, 0, 2);
            Assert.AreEqual(4, result.MaxSeries);
            Assert.IsFalse(result.IsBroken, "expected not broken series");
            Assert.IsTrue(result.IsCapped, "expected capped series");
        }

        /*
4|_|_|S|_|_|_|_|_
3|_|_|_|0|_|_|_|_
2|_|_|_|_|_|_|_|_
1|_|_|_|_|_|0|_|_
0|_|_|_|_|_|_|x|_
 |0|1|2|3|4|5|6|7
*/
        [TestMethod]
        public void GetStatsInDirection_DiagonalBrokenCapped4()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2, 4},
                {3, 3},
                {5, 1},
            });
            AddOpponentMoves(new int[,]
            {
                {6,0}
            }, ref board);
            var target = new GameBoardAnalyzer(new GameBoard(board));
            RowStats result = target.GetStatsInDirection(new Cell(2, 4, 2), 1, -1, 2);
            Assert.AreEqual(3, result.MaxSeries);
            Assert.IsTrue(result.IsBroken, "expected broken series");
            Assert.IsTrue(result.IsCapped, "expected capped series");
        }

        /*
4|_|_|S|_|_|_|_|_
3|_|_|_|0|_|_|_|_
2|_|_|_|_|0|_|_|_
1|_|_|_|_|_|0|_|_
0|_|_|_|_|_|_|x|_
 |0|1|2|3|4|5|6|7
*/
        [TestMethod]
        public void GetStatsInDirection_DiagonalCapped4()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2, 4},
                {3, 3},
                {4, 2},
                {5, 1},
            });
            AddOpponentMoves(new int[,]
            {
                {6,0}
            }, ref board);
            var target = new GameBoardAnalyzer(new GameBoard(board));
            RowStats result = target.GetStatsInDirection(new Cell(2, 4, 2), 1, -1, 2);
            Assert.AreEqual(4, result.MaxSeries);
            Assert.IsFalse(result.IsBroken, "expected non broken series");
            Assert.IsTrue(result.IsCapped, "expected capped series");
        }

        /*
4|_|_|_|_|_|_|_|_
3|_|_|_|_|_|_|_|_
2|_|_|S|0|0|_|x|_
1|_|_|_|_|_|_|_|_
0|_|_|_|_|_|_|_|_
 |0|1|2|3|4|5|6|7
*/
        [TestMethod]
        public void GetStatsInDirection_UncappedButClosed()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
                {2, 2},
                {3, 2},
                {4, 2},
            });
            AddOpponentMoves(new int[,]
            {
                {6,2}
            }, ref board);
            var target = new GameBoardAnalyzer(new GameBoard(board));
            RowStats result = target.GetStatsInDirection(new Cell(2, 2, 2), 1, 0, 2);
            Assert.AreEqual(3, result.MaxSeries);
            Assert.IsFalse(result.IsBroken, "expected non broken series");
            Assert.IsFalse(result.IsCapped, "expected un capped series");
        }

        /*
4|_|_|_|_|_|_|_|_
3|_|_|_|_|_|_|_|_
2|_|_|X|_|_|_|_|_
1|_|_|_|_|_|_|_|_
0|_|_|_|_|_|_|_|_
 |0|1|2|3|4|5|6|7
*/
        [TestMethod]
        public void GetStatsInDirection_OnlyOpponentMark()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
            });
            AddOpponentMoves(new int[,]
            {
                {2,2}
            }, ref board);
            var target = new GameBoardAnalyzer(new GameBoard(board));
            RowStats result = target.GetStatsInDirection(new Cell(2, 2), 1, 0, 2);
            Assert.AreEqual(0, result.MaxSeries);
            Assert.IsFalse(result.IsBroken, "expected non broken series");
            Assert.IsTrue(result.IsCapped, "expected capped series");
        }

           /*
4|_|_|_|_|_|_|_|_
3|_|_|_|_|_|_|_|_
2|_|_|_|_|_|_|_|_
1|_|_|_|_|_|_|_|_
0|_|_|_|_|_|_|_|_
 |0|1|2|3|4|5|6|7
*/
        [TestMethod]
        public void GetStatsInDirection_NoMarks()
        {
            //arrange
            var board = GetBoard(new int[,]
            {
            });
            var target = new GameBoardAnalyzer(new GameBoard(board));
            RowStats result = target.GetStatsInDirection(new Cell(2, 2), 1, 0, 2);
            Assert.AreEqual(0, result.MaxSeries);
            Assert.IsFalse(result.IsBroken, "expected non broken series");
            Assert.IsFalse(result.IsCapped, "expected uncapped series");
        }
    }
}
