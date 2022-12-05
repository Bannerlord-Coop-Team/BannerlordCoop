using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;
using Common.Extensions;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using System.Collections.Generic;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterTraitsSerializationTest
    {
        [Fact]
        public void CharacterTraits_Serialize()
        {
            CharacterTraits CharacterTraits = new CharacterTraits();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterTraitsBinaryPackage package = new CharacterTraitsBinaryPackage(CharacterTraits, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _attributes = typeof(PropertyOwner<TraitObject>).GetField("_attributes", BindingFlags.Instance | BindingFlags.NonPublic);
        [Fact]
        public void CharacterTraits_Full_Serialization()
        {
            CharacterTraits characterTraits = new CharacterTraits();

            Dictionary<TraitObject, int> traits = new Dictionary<TraitObject, int>
            {
                { new TraitObject("Trait1"), 3 },
                { new TraitObject("Trait2"), 7 },
            };
            _attributes.SetValue(characterTraits, traits);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterTraitsBinaryPackage package = new CharacterTraitsBinaryPackage(characterTraits, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterTraitsBinaryPackage>(obj);

            CharacterTraitsBinaryPackage returnedPackage = (CharacterTraitsBinaryPackage)obj;

            CharacterTraits newCharacterTraits = returnedPackage.Unpack<CharacterTraits>();

            Assert.Equal(characterTraits.Id, newCharacterTraits.Id);
            Assert.Equal(characterTraits.StringId, newCharacterTraits.StringId);
            Assert.Equal(characterTraits.IsReady, newCharacterTraits.IsReady);

            Dictionary<TraitObject, int> newTraits = (Dictionary<TraitObject, int>)_attributes.GetValue(newCharacterTraits);

            Assert.Equal(traits.ToString(), newTraits.ToString());
        }
    }
}
