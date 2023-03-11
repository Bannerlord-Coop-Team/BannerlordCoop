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
    public class CharacterAttributesSerializationTest
    {
        public CharacterAttributesSerializationTest()
        {
            GameBootStrap.Initialize();
        }
        [Fact]
        public void CharacterAttributes_Serialize()
        {
            CharacterAttributes CharacterAttributes = new CharacterAttributes();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterAttributesBinaryPackage package = new CharacterAttributesBinaryPackage(CharacterAttributes, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _attributes = typeof(PropertyOwner<CharacterAttribute>).GetField("_attributes", BindingFlags.Instance | BindingFlags.NonPublic);
        [Fact]
        public void CharacterAttributes_Full_Serialization()
        {
            CharacterAttributes characterAttributes = new CharacterAttributes();

            // Setup non default values for characterAttributes
            CharacterAttribute attr1 = new CharacterAttribute("Attr1");
            CharacterAttribute attr2 = new CharacterAttribute("Attr2");

            MBObjectManager.Instance.RegisterObject(attr1);
            MBObjectManager.Instance.RegisterObject(attr2);

            Dictionary<CharacterAttribute, int> Attributes = new Dictionary<CharacterAttribute, int>
            {
                { attr1, 3 },
                { attr2, 7 },
            };
            _attributes.SetValue(characterAttributes, Attributes);

            // Setup serialization for characterAttributes
            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterAttributesBinaryPackage package = new CharacterAttributesBinaryPackage(characterAttributes, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterAttributesBinaryPackage>(obj);

            CharacterAttributesBinaryPackage returnedPackage = (CharacterAttributesBinaryPackage)obj;

            CharacterAttributes newCharacterAttributes = returnedPackage.Unpack<CharacterAttributes>();

            // Verify values are equal
            Assert.Equal(characterAttributes.Id, newCharacterAttributes.Id);
            Assert.Equal(characterAttributes.StringId, newCharacterAttributes.StringId);
            Assert.Equal(characterAttributes.IsReady, newCharacterAttributes.IsReady);

            Dictionary<CharacterAttribute, int> newAttributes = (Dictionary<CharacterAttribute, int>)_attributes.GetValue(newCharacterAttributes);

            Assert.Equal(Attributes.ToString(), newAttributes.ToString());
        }
    }
}
