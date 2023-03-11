using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MobilePartySerializationTest
    {
        public MobilePartySerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void MobileParty_Serialize()
        {
            MobileParty mobilePartyObject = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));           

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MobilePartyBinaryPackage package = new MobilePartyBinaryPackage(mobilePartyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MobileParty_Full_Serialization()
        {
            MobileParty mobilePartyObject = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            // Assign non default values to mobile party
            mobilePartyObject.SetCustomName(new TextObject("Custom Name"));
            mobilePartyObject.Aggressiveness = 56;
            mobilePartyObject.IsActive = true;

            Hero surgeon = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            surgeon.StringId = "My Surgeon";
            MBObjectManager.Instance.RegisterObject(surgeon);

            MobilePartyBinaryPackage.MobileParty_Surgeon.SetValue(mobilePartyObject, surgeon);

            // Setup serialization for mobilePartyObject
            BinaryPackageFactory factory = new BinaryPackageFactory();
            MobilePartyBinaryPackage package = new MobilePartyBinaryPackage(mobilePartyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MobilePartyBinaryPackage>(obj);

            MobilePartyBinaryPackage returnedPackage = (MobilePartyBinaryPackage)obj;

            MobileParty newMobilePartyObject = returnedPackage.Unpack<MobileParty>();

            // Verify party values
            Assert.True(mobilePartyObject.Name.Equals(newMobilePartyObject.Name));
            Assert.Equal(mobilePartyObject.Aggressiveness, newMobilePartyObject.Aggressiveness);
            Assert.Equal(mobilePartyObject.IsActive, newMobilePartyObject.IsActive);
            Assert.Same(mobilePartyObject.EffectiveSurgeon, newMobilePartyObject.EffectiveSurgeon);
        }
    }
}
