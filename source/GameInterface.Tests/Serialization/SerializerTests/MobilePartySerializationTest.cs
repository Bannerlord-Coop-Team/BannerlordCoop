using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using GameInterface.Tests.Bootstrap;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

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

            mobilePartyObject.SetCustomName(new TextObject("Custom Name"));
            mobilePartyObject.Aggressiveness = 56;
            mobilePartyObject.IsActive = true;

            Hero surgeon = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            surgeon.StringId = "My Surgeon";
            MBObjectManager.Instance.RegisterObject(surgeon);

            MobilePartyBinaryPackage.MobileParty_Surgeon.SetValue(mobilePartyObject, surgeon);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MobilePartyBinaryPackage package = new MobilePartyBinaryPackage(mobilePartyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MobilePartyBinaryPackage>(obj);

            MobilePartyBinaryPackage returnedPackage = (MobilePartyBinaryPackage)obj;

            MobileParty newMobilePartyObject = returnedPackage.Unpack<MobileParty>();

            Assert.True(mobilePartyObject.Name.Equals(newMobilePartyObject.Name));
            Assert.Equal(mobilePartyObject.Aggressiveness, newMobilePartyObject.Aggressiveness);
            Assert.Equal(mobilePartyObject.IsActive, newMobilePartyObject.IsActive);
            Assert.Same(mobilePartyObject.EffectiveSurgeon, newMobilePartyObject.EffectiveSurgeon);
        }
    }
}
