using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterPerksSerializationTest
    {

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
            CharacterPerks CharacterPerks = new CharacterPerks();

            Dictionary<PerkObject, int> perks = new Dictionary<PerkObject, int>
            {
                { new PerkObject("MyPerk"), 5 },
                { new PerkObject("MyPerk2"), 6 },
            };

            _attributes.SetValue(CharacterPerks, perks);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterPerksBinaryPackage package = new CharacterPerksBinaryPackage(CharacterPerks, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterPerksBinaryPackage>(obj);

            CharacterPerksBinaryPackage returnedPackage = (CharacterPerksBinaryPackage)obj;

            CharacterPerks newCharacterPerks = returnedPackage.Unpack<CharacterPerks>();

            Assert.Equal(CharacterPerks.StringId, CharacterPerks.StringId);
            Assert.Equal(CharacterPerks.Id, CharacterPerks.Id);
            Assert.Equal(CharacterPerks.IsReady, newCharacterPerks.IsReady);

            Dictionary<PerkObject, int> newPerks = (Dictionary<PerkObject, int>)_attributes.GetValue(newCharacterPerks);

            Assert.Equal(perks.ToString(), newPerks.ToString());
        }
    }
}
