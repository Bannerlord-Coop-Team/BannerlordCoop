using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class VillageSerializationTest
    {
        ILifetimeScope container;
        public VillageSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void Village_Full_Serialization()
        {
            Village testVillage = new Village();

            var factory = container.Resolve<IBinaryPackageFactory>();
            VillageBinaryPackage package = new VillageBinaryPackage(testVillage, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<VillageBinaryPackage>(obj);

            VillageBinaryPackage returnedPackage = (VillageBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
