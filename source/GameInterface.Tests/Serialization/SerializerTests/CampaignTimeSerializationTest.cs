using Coop.Mod.Extentions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CampaignTimeSerializationTest
    {
        [Fact]
        public void CampaignTime_Serialize()
        {
            CampaignTime CampaignTime = new CampaignTime();

            BinaryPackageFactory factory = new BinaryPackageFactory();
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

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CampaignTimeBinaryPackage package = new CampaignTimeBinaryPackage(CampaignTime, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CampaignTimeBinaryPackage>(obj);

            CampaignTimeBinaryPackage returnedPackage = (CampaignTimeBinaryPackage)obj;

            CampaignTime newCampaignTime = returnedPackage.Unpack<CampaignTime>();

            Assert.Equal(CampaignTime.GetNumTicks(), newCampaignTime.GetNumTicks());
        }
    }
}
