using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Tests.Registry
{
    public class TwoWayDictionaryTests
    {
        [Fact]
        public void Add()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.Equal(1, testDict.Count);
            Assert.Equal(value, testDict[guid]);
            Assert.Equal(guid, testDict[value]);
        }

        [Fact]
        public void Add_ExistingT1()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.False(testDict.Add(guid, 2));
        }

        [Fact]
        public void Add_ExistingT2()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.False(testDict.Add(Guid.NewGuid(), value));
        }

        [Fact]
        public void Remove_ByT1()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.True(testDict.Remove(guid));
            Assert.Equal(0, testDict.Count);
        }

        [Fact]
        public void Remove_ByT2()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.True(testDict.Remove(value));
            Assert.Equal(0, testDict.Count);
        }

        [Fact]
        public void Remove_NonExistantT1()
        {
            Guid guid = Guid.NewGuid();
            var testDict = new TwoWayDictionary<Guid, int>();

            Assert.False(testDict.Remove(guid));
            Assert.Equal(0, testDict.Count);
        }

        [Fact]
        public void Remove_NonExistantT2()
        {
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>();

            Assert.False(testDict.Remove(value));
            Assert.Equal(0, testDict.Count);
        }

        [Fact]
        public void ContainsKey_T1()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.True(testDict.Contains(guid));
        }

        [Fact]
        public void ContainsKey_T2()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.True(testDict.Contains(value));
        }

        [Fact]
        public void ContainsKey_NonExistantT1()
        {
            Guid guid = Guid.NewGuid();
            var testDict = new TwoWayDictionary<Guid, int>();

            Assert.False(testDict.Contains(guid));
        }

        [Fact]
        public void ContainsKey_NonExistantT2()
        {
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>();

            Assert.False(testDict.Contains(value));
        }

        [Fact]
        public void Clear()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.Equal(1, testDict.Count);

            testDict.Clear();

            Assert.Equal(0, testDict.Count);
        }

        [Fact]
        public void Clear_Empty()
        {
            var testDict = new TwoWayDictionary<Guid, int>();

            Assert.Equal(0, testDict.Count);

            testDict.Clear();

            Assert.Equal(0, testDict.Count);
        }

        [Fact]
        public void TryGetValue_T1()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.True(testDict.TryGetValue(guid, out int resolvedItem));

            Assert.Equal(value, resolvedItem);
        }

        [Fact]
        public void TryGetValue_T2()
        {
            Guid guid = Guid.NewGuid();
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>
            {
                { guid, value }
            };

            Assert.True(testDict.TryGetValue(value, out Guid resolvedGuid));

            Assert.Equal(guid, resolvedGuid);
        }

        [Fact]
        public void TryGetValue_NonExistantT1()
        {
            Guid guid = Guid.NewGuid();
            var testDict = new TwoWayDictionary<Guid, int>();

            Assert.False(testDict.TryGetValue(guid, out int _));
        }

        [Fact]
        public void TryGetValue_NonExistantT2()
        {
            int value = 1;
            var testDict = new TwoWayDictionary<Guid, int>();

            Assert.False(testDict.TryGetValue(value, out Guid _));
        }
    }
}
