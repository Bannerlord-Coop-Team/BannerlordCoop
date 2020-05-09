using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Common;
using Xunit;

namespace Coop.Tests
{
    public class MovingAverage_Test
    {
        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void TestDifferentWindowSizes(int iSize)
        {
            MovingAverage m_Avg = new MovingAverage(iSize);
            Queue<long> queue = new Queue<long>();
            Random random = new Random(0);
            for (int i = 0; i < 10 * iSize; ++i)
            {
                long value = random.Next();
                queue.Enqueue(i);
                if (queue.Count > iSize)
                {
                    queue.Dequeue();
                }

                Assert.Equal(queue.Average(), m_Avg.Push(i));
            }
        }

        [Fact]
        public void TestAverage()
        {
            MovingAverage avg = new MovingAverage(5);
            Assert.Equal(4, avg.Push(4));
            Assert.Equal(6, avg.Push(8));
            Assert.Equal(5, avg.Push(3));
            Assert.Equal(5, avg.Push(5));
        }
    }
}
