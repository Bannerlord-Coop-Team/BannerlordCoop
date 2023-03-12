using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterPerksSerializationTest
    {
        public CharacterPerksSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void CharacterPerks_Serialize()
        {
            CharacterPerks CharacterPerks = new CharacterPerks();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterPerksBinaryPackage package = new CharacterPerksBinaryPackage(CharacterPerks, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _attributes = typeof(PropertyOwner<PerkObject>).GetField("_attributes", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void CharacterPerks_Full_Serialization()
        {
            CharacterPerks characterPerks = new CharacterPerks();

            characterPerks.StringId = "myCharacterPerks";

            MBObjectManager.Instance.RegisterObject(characterPerks);

            PerkObject perk1 = new PerkObject("MyPerk");
            PerkObject perk2 = new PerkObject("MyPerk2");
            PerkObject perk3 = new PerkObject("MyPerk3");

            MBObjectManager.Instance.RegisterObject(perk1);
            MBObjectManager.Instance.RegisterObject(perk2);
            MBObjectManager.Instance.RegisterObject(perk3);

            Dictionary<PerkObject, int> perks = new Dictionary<PerkObject, int>
            {
                { perk1, 5 },
                { perk2, 6 },
                { perk3, 7 }
            };

            _attributes.SetValue(characterPerks, perks);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterPerksBinaryPackage package = new CharacterPerksBinaryPackage(characterPerks, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterPerksBinaryPackage>(obj);

            CharacterPerksBinaryPackage returnedPackage = (CharacterPerksBinaryPackage)obj;

            CharacterPerks newCharacterPerks = returnedPackage.Unpack<CharacterPerks>();

            Assert.Equal(characterPerks.StringId, characterPerks.StringId);
            Assert.Equal(characterPerks.Id, characterPerks.Id);
            Assert.Equal(characterPerks.IsReady, newCharacterPerks.IsReady);

            Dictionary<PerkObject, int> newPerks = (Dictionary<PerkObject, int>)_attributes.GetValue(newCharacterPerks);

            Assert.Equal(perks.ToString(), newPerks.ToString());
        }
    }
}
