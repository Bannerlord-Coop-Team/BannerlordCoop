using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using System.Collections.Generic;
using TaleWorlds.ObjectSystem;
using GameInterface.Tests.Bootstrap;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterTraitsSerializationTest
    {
        public CharacterTraitsSerializationTest()
        {
            GameBootStrap.Initialize();
        }
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

            TraitObject trait1 = new TraitObject("Trait1");
            TraitObject trait2 = new TraitObject("Trait2");

            MBObjectManager.Instance.RegisterObject(trait1);
            MBObjectManager.Instance.RegisterObject(trait2);

            Dictionary<TraitObject, int> traits = new Dictionary<TraitObject, int>
            {
                { trait1, 3 },
                { trait2, 7 },
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
