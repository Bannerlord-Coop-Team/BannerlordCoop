using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterObjectSerializationTest
    {
        public CharacterObjectSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void CharacterObject_Serialize()
        {
            CharacterObject CharacterObject = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterObjectBinaryPackage package = new CharacterObjectBinaryPackage(CharacterObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CharacterObject_Full_Serialization()
        {
            CharacterObject CharacterObject = new CharacterObject();

            CharacterObject[] characterMembers = new CharacterObject[]
            {
                new CharacterObject(),
                new CharacterObject(),
                new CharacterObject(),
                new CharacterObject(),
            };

            for (int i = 0; i < characterMembers.Length; i++)
            {
                var character = characterMembers[i];
                character.StringId = $"Character_{i}";
                MBObjectManager.Instance.RegisterObject(character);
            }

            CharacterObjectBinaryPackage.CharacterObject_battleEquipmentTemplate.SetValue(CharacterObject, characterMembers[0]);
            CharacterObjectBinaryPackage.CharacterObject_civilianEquipmentTemplate.SetValue(CharacterObject, characterMembers[1]);
            CharacterObjectBinaryPackage.CharacterObject_originCharacter.SetValue(CharacterObject, characterMembers[2]);
            CharacterObjectBinaryPackage.CharacterObject_UpgradeTargets.SetValue(CharacterObject, characterMembers);

            BinaryPackageFactory factory = new BinaryPackageFactory();

            byte[] bytes = BinaryFormatterSerializer.Serialize(factory.GetBinaryPackage(CharacterObject));

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterObjectBinaryPackage>(obj);

            CharacterObjectBinaryPackage returnedPackage = (CharacterObjectBinaryPackage)obj;

            CharacterObject newCharacterObject = returnedPackage.Unpack<CharacterObject>();

            Assert.Same(characterMembers[0], CharacterObjectBinaryPackage.CharacterObject_battleEquipmentTemplate.GetValue(newCharacterObject));
            Assert.Same(characterMembers[1], CharacterObjectBinaryPackage.CharacterObject_civilianEquipmentTemplate.GetValue(newCharacterObject));
            Assert.Same(characterMembers[2], CharacterObjectBinaryPackage.CharacterObject_originCharacter.GetValue(newCharacterObject));

            CharacterObject[] characterObjects = newCharacterObject.UpgradeTargets;

            Assert.Equal(CharacterObject.UpgradeTargets.Length, characterObjects.Length);

            foreach(var vals in characterMembers.Zip(characterObjects, (v1, v2) => (v1, v2)))
            {
                Assert.Same(vals.v1, vals.v2);
            }
        }

        [Fact]
        public void CharacterObject_StringId_Serialization()
        {
            CharacterObject CharacterObject = MBObjectManager.Instance.CreateObject<CharacterObject>();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterObjectBinaryPackage package = new CharacterObjectBinaryPackage(CharacterObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterObjectBinaryPackage>(obj);

            CharacterObjectBinaryPackage returnedPackage = (CharacterObjectBinaryPackage)obj;

            CharacterObject newCharacterObject = returnedPackage.Unpack<CharacterObject>();

            Assert.Same(CharacterObject, newCharacterObject);
        }
    }
}
