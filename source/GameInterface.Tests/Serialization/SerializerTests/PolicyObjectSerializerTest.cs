using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using TaleWorlds.CampaignSystem;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PolicyObjectSerializationTest
    {
        IContainer container;
        public PolicyObjectSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void PolicyObject_Serialize()
        {
            PolicyObject testPolicyObject = new PolicyObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            PolicyObjectBinaryPackage package = new PolicyObjectBinaryPackage(testPolicyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void PolicyObject_Full_Serialization()
        {
            PolicyObject testPolicyObject = new PolicyObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            PolicyObjectBinaryPackage package = new PolicyObjectBinaryPackage(testPolicyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<PolicyObjectBinaryPackage>(obj);

            PolicyObjectBinaryPackage returnedPackage = (PolicyObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
