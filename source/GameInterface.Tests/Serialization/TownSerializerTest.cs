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
        ILifetimeScope container;
        public TownSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
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
