using GameInterface.Serialization;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization
{
    public class DictTest
    {
        private readonly ITestOutputHelper output;

        public DictTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Test_DuplicateClassAdd()
        {
            Dictionary<ObjectAndType, int> tDict = new Dictionary<ObjectAndType, int>();
            int i = 5;

            ObjectAndType OAT = new ObjectAndType(i);
            ObjectAndType OAT2 = new ObjectAndType(i);

            Assert.Equal(OAT, OAT2);
            Assert.Equal(OAT.GetHashCode(), OAT2.GetHashCode());


            tDict.Add(OAT, 1);

            Assert.True(tDict.ContainsKey(OAT2));

            Assert.Throws<ArgumentException>(() => tDict.Add(OAT2, 2));
        }
    }
}