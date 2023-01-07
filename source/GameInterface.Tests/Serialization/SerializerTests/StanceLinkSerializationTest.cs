using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class StanceLinkSerializationTest
    {
        public StanceLinkSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void StanceLink_Serialize()
        {
            StanceLink stanceLink = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            StanceLinkBinaryPackage package = new StanceLinkBinaryPackage(stanceLink, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void StanceLink_Full_Serialization()
        {
            StanceLink stanceLink = (StanceLink)FormatterServices.GetUninitializedObject(typeof(StanceLink));

            // Setup stanceLink factions
            Clan clan1 = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            Clan clan2 = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            clan1.StringId = "clan1";
            clan2.StringId = "clan2";

            MBObjectManager.Instance.RegisterObject(clan1);
            MBObjectManager.Instance.RegisterObject(clan2);

            StanceLinkBinaryPackage.StanceLink_Faction1.SetValue(stanceLink, clan1);
            StanceLinkBinaryPackage.StanceLink_Faction2.SetValue(stanceLink, clan2);

            // Serialize stanceLink
            BinaryPackageFactory factory = new BinaryPackageFactory();
            StanceLinkBinaryPackage package = new StanceLinkBinaryPackage(stanceLink, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<StanceLinkBinaryPackage>(obj);

            StanceLinkBinaryPackage returnedPackage = (StanceLinkBinaryPackage)obj;

            StanceLink newStanceLink = returnedPackage.Unpack<StanceLink>();

            Assert.Equal(stanceLink.Faction1, newStanceLink.Faction1);
            Assert.Equal(stanceLink.Faction2, newStanceLink.Faction2);
            Assert.Equal(stanceLink.Casualties1, newStanceLink.Casualties1);
        }
    }
}
