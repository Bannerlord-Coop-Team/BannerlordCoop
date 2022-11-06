using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests.ProofOfConcept
{
    public class GenericSerializationTests
    {
        [Fact]
        public void CircularReference_Full_Serialization()
        {
            TestClassA testClassA = new TestClassA();

            ClassABinaryPackage package = SerializerStore.GetSerializer<ClassABinaryPackage>(testClassA);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            ClassABinaryPackage deserialized = BinaryFormatterSerializer.Deserialize<ClassABinaryPackage>(bytes);

            TestClassA classA = deserialized.Deserialize();

            Assert.Same(classA, classA.testClassB.testClassA);
        }
    }
}
