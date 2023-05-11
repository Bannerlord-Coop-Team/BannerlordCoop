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
    public class SettlementSerializationTest
    {
        IContainer container;
        public SettlementSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Settlement_Serialize()
        {
            Settlement testSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));

            var factory = container.Resolve<IBinaryPackageFactory>();
            SettlementBinaryPackage package = new SettlementBinaryPackage(testSettlement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Settlement_Full_Serialization()
        {
            Settlement testSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));

            var factory = container.Resolve<IBinaryPackageFactory>();
            SettlementBinaryPackage package = new SettlementBinaryPackage(testSettlement, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<SettlementBinaryPackage>(obj);

            SettlementBinaryPackage returnedPackage = (SettlementBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
