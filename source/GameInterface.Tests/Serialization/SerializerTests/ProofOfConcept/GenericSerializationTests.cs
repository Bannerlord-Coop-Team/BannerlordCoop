using GameInterface.Serialization;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests.ProofOfConcept
{
    public class GenericSerializationTests
    {
        [Fact]
        public void CircularReference_Full_Serialization()
        {
            TestClassA testClassA = new TestClassA();

            BinaryPackageFactory factory = new BinaryPackageFactory();

            ClassABinaryPackage package = factory.GetBinaryPackage<ClassABinaryPackage>(testClassA);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            ClassABinaryPackage deserialized = BinaryFormatterSerializer.Deserialize<ClassABinaryPackage>(bytes);

            TestClassA classA = deserialized.Unpack<TestClassA>();

            Assert.Same(classA, classA.testClassB.testClassA);
        }
    }
}
