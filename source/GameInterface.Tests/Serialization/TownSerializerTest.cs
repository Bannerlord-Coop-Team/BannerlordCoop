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

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Town_Full_Serialization()
        {
            Town testTown = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

            var factory = container.Resolve<IBinaryPackageFactory>();
            TownBinaryPackage package = new TownBinaryPackage(testTown, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<TownBinaryPackage>(obj);

            TownBinaryPackage returnedPackage = (TownBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
