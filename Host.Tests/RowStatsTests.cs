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
    public class RowStatsTests
    {
        //" 00S0 "
        [TestMethod]
        public void Merge_2UnCappedDoubles_Uncapped4()
        {
            var first = new RowStats();
            var second = new RowStats();
            first.MaxSeries = 2;
            second.MaxSeries = 2;

            RowStats result = first + second;

            Assert.AreEqual(4, result.MaxSeries, "Uncapped4 expected");
            Assert.IsFalse(result.IsCapped);
            Assert.IsFalse(result.IsBroken);
        }

        //"X00S0X"
        [TestMethod]
        public void Merge_2CappedDoubles_None()
        {
            var first = new RowStats();
            var second = new RowStats();
            first.MaxSeries = 2;
            second.MaxSeries = 2;
            first.IsCapped = second.IsCapped = true;

            RowStats result = first + second;

            Assert.AreEqual(0, result.MaxSeries, "Expected no value");
        }

        //"X00S0 "
        [TestMethod]
        public void Merge_Capped2UnCapped2_Capped4()
        {
            var first = new RowStats();
            var second = new RowStats();
            first.MaxSeries = 2;
            second.MaxSeries = 2;
            first.IsCapped = true;

            RowStats result = first + second;

            Assert.AreEqual(4, result.MaxSeries, "Expected 4");
            Assert.IsTrue(result.IsCapped);
            Assert.IsFalse(result.IsBroken);
        }

        //" 00 0S 0 "
        [TestMethod]
        public void Merge_Broken4Broken3_UnCapped2()
        {
            var first = new RowStats();
            var second = new RowStats();
            first.MaxSeries = 3;
            second.MaxSeries = 2;
            first.IsBroken = second.IsBroken = true;

            RowStats result = first + second;

            Assert.AreEqual(2, result.MaxSeries, "Expected 2");
            Assert.IsFalse(result.IsCapped);
            Assert.IsFalse(result.IsBroken);
        }
    }
}
