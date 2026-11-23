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
    public class SettlementSerializationTest
    {
        ILifetimeScope container;
        public SettlementSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void Settlement_Full_Serialization()
        {
            Settlement testSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));

            var factory = container.Resolve<IBinaryPackageFactory>();
            SettlementBinaryPackage package = new SettlementBinaryPackage(testSettlement, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<SettlementBinaryPackage>(obj);

            SettlementBinaryPackage returnedPackage = (SettlementBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
