using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Tests.Serialization.SerializerTests.ProofOfConcept
{
    internal class BasicClass
    {
        int i;

        public BasicClass(int i)
        {
            this.i = i;
        }
    }

    internal class TestClassA
    {
        public TestClassB testClassB;

        public TestClassA()
        {
            testClassB = new TestClassB(this);
        }
    }

    internal class TestClassB
    {
        public TestClassA testClassA;

        public TestClassB(TestClassA testClassA)
        {
            this.testClassA = testClassA;
        }
    }

    internal class TestClassList
    {
        public readonly List<BasicClass> basicClasses = new List<BasicClass>();
    }

    internal class StaggeredArrayClass
    {
        readonly BasicClass[][] basicClasses;

        public StaggeredArrayClass(BasicClass[][] basicClasses)
        {
            this.basicClasses = basicClasses;
        }
    }
}
