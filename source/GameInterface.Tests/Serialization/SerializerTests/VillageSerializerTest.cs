using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class VillageSerializationTest
    {
        IContainer container;
        public VillageSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Village_Serialize()
        {
            Village testVillage = (Village)FormatterServices.GetUninitializedObject(typeof(Village));

            var factory = container.Resolve<IBinaryPackageFactory>();
            VillageBinaryPackage package = new VillageBinaryPackage(testVillage, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Village_Full_Serialization()
        {
            Village testVillage = new Village();

            var factory = container.Resolve<IBinaryPackageFactory>();
            VillageBinaryPackage package = new VillageBinaryPackage(testVillage, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<VillageBinaryPackage>(obj);

            VillageBinaryPackage returnedPackage = (VillageBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
