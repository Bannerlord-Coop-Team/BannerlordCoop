using Autofac;
using GameInterface.Serialization;
using GameInterface.Tests.Bootstrap.Modules;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests.ProofOfConcept
{
    public class GenericSerializationTests
    {
        IContainer container;
        public GenericSerializationTests()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CircularReference_Full_Serialization()
        {
            TestClassA testClassA = new TestClassA();

            var factory = container.Resolve<IBinaryPackageFactory>();

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
