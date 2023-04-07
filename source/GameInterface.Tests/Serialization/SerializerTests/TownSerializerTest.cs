using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements;
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TownSerializationTest
    {
        IContainer container;
        public TownSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Town_Serialize()
        {
            Town testTown = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

            var factory = container.Resolve<IBinaryPackageFactory>();
            TownBinaryPackage package = new TownBinaryPackage(testTown, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Town_Full_Serialization()
        {
            Town testTown = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

            var factory = container.Resolve<IBinaryPackageFactory>();
            TownBinaryPackage package = new TownBinaryPackage(testTown, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TownBinaryPackage>(obj);

            TownBinaryPackage returnedPackage = (TownBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
