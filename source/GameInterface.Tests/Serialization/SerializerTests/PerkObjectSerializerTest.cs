using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PerkObjectSerializationTest
    {
        IContainer container;
        public PerkObjectSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void PerkObject_Serialize()
        {
            PerkObject testPerkObject = new PerkObject("test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            PerkObjectBinaryPackage package = new PerkObjectBinaryPackage(testPerkObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void PerkObject_Full_Serialization()
        {
            PerkObject testPerkObject = new PerkObject("test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            PerkObjectBinaryPackage package = new PerkObjectBinaryPackage(testPerkObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<PerkObjectBinaryPackage>(obj);

            PerkObjectBinaryPackage returnedPackage = (PerkObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
