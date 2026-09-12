using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.CampaignSystem;
using Xunit;

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

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void PolicyObject_Full_Serialization()
        {
            PolicyObject testPolicyObject = new PolicyObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            PolicyObjectBinaryPackage package = new PolicyObjectBinaryPackage(testPolicyObject, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<PolicyObjectBinaryPackage>(obj);

            PolicyObjectBinaryPackage returnedPackage = (PolicyObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
