using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Tests.Bootstrap.Modules;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests.ProofOfConcept
{
    public class GenericSerializationTests
    {
        ILifetimeScope container;
        public GenericSerializationTests()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void CustomPackageOutsideProductionAssembly_IsRejected()
        {
            TestClassA testClassA = new TestClassA();

            var factory = container.Resolve<IBinaryPackageFactory>();

            ClassABinaryPackage package = factory.GetBinaryPackage<ClassABinaryPackage>(testClassA);

            package.Pack();

            Assert.Throws<System.Runtime.Serialization.SerializationException>(() =>
                BinaryPackageSerializer.Serialize(package));
        }
    }
}
