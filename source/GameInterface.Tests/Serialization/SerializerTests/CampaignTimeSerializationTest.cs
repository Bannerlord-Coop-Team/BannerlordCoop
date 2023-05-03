using Autofac;
using Coop.Mod.Extentions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.CampaignSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CampaignTimeSerializationTest
    {
        IContainer container;
        public CampaignTimeSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CampaignTime_Serialize()
        {
            CampaignTime CampaignTime = new CampaignTime();

            var factory = container.Resolve<IBinaryPackageFactory>();
            CampaignTimeBinaryPackage package = new CampaignTimeBinaryPackage(CampaignTime, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CampaignTime_Full_Serialization()
        {
            CampaignTime CampaignTime = new CampaignTime();
            CampaignTime.SetNumTicks(1111);

            var factory = container.Resolve<IBinaryPackageFactory>();
            CampaignTimeBinaryPackage package = new CampaignTimeBinaryPackage(CampaignTime, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CampaignTimeBinaryPackage>(obj);

            CampaignTimeBinaryPackage returnedPackage = (CampaignTimeBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CampaignTime newCampaignTime = returnedPackage.Unpack<CampaignTime>(deserializeFactory);

            Assert.Equal(CampaignTime.GetNumTicks(), newCampaignTime.GetNumTicks());
        }
    }
}
