using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HeroLastSeenInfoSerializationTest
    {
        [Fact]
        public void HeroLastSeenInfo_Serialize()
        {
            Hero.HeroLastSeenInformation heroLastSeenInformation = new Hero.HeroLastSeenInformation();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            HeroLastSeenInfoBinaryPackage package = new HeroLastSeenInfoBinaryPackage(heroLastSeenInformation, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void HeroLastSeenInfo_Full_Serialization()
        {
            Hero.HeroLastSeenInformation heroLastSeenInformation = new Hero.HeroLastSeenInformation();
            heroLastSeenInformation.LastSeenDate = new CampaignTime();
            heroLastSeenInformation.IsNearbySettlement = false;

            BinaryPackageFactory factory = new BinaryPackageFactory();
            HeroLastSeenInfoBinaryPackage package = new HeroLastSeenInfoBinaryPackage(heroLastSeenInformation, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HeroLastSeenInfoBinaryPackage>(obj);

            HeroLastSeenInfoBinaryPackage returnedPackage = (HeroLastSeenInfoBinaryPackage)obj;

            Hero.HeroLastSeenInformation newStaticBodyProperties = returnedPackage.Unpack<Hero.HeroLastSeenInformation>();

            Assert.Equal(heroLastSeenInformation.LastSeenDate, newStaticBodyProperties.LastSeenDate);
            Assert.Equal(heroLastSeenInformation.IsNearbySettlement, newStaticBodyProperties.IsNearbySettlement);
        }
    }
}
