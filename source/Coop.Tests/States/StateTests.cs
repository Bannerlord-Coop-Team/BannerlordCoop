using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;


namespace Coop.Tests
{
    class TestClass
    {
        public void MyMethod() { }
    }

    public class StateTests
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();
        /// <summary>
        /// Do delagates keep objects alive?
        /// </summary>
        [Fact]
        public void DelagateKeepAliveTest()
        {
            TestClass c = new TestClass();
            _subscribers.Add(typeof(TestClass), new List<Delegate> { new Action(c.MyMethod) });
            c = null;
            GC.Collect();
            Assert.Equal(0, _subscribers[typeof(TestClass)].Count);
        }
    }
}
