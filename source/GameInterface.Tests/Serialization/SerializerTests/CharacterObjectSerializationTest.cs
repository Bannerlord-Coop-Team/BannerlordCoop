using Autofac;
using Common;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterObjectSerializationTest
    {
        IContainer container;
        public CharacterObjectSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        private static readonly FieldInfo BasicCharacterObject_basicName = typeof(BasicCharacterObject).GetField("_basicName", BindingFlags.NonPublic | BindingFlags.Instance);

        [Fact]
        public void CharacterObject_Serialize()
        {
            CharacterObject CharacterObject = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));

            BasicCharacterObject_basicName.SetValue(CharacterObject, new TextObject("Test Name"));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CharacterObjectBinaryPackage package = new CharacterObjectBinaryPackage(CharacterObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CharacterObject_Full_Serialization()
        {
            CharacterObject CharacterObject = new CharacterObject();

            BasicCharacterObject_basicName.SetValue(CharacterObject, new TextObject("Test Name"));

            CharacterObject[] characterMembers = new CharacterObject[]
            {
                new CharacterObject(),
                new CharacterObject(),
                new CharacterObject(),
                new CharacterObject(),
            };

            var objectManager = container.Resolve<IObjectManager>();

            for (int i = 0; i < characterMembers.Length; i++)
            {
                var character = characterMembers[i];
                character.StringId = $"Character_{i}";
                objectManager.AddExisting(character.StringId, character);
            }

            CharacterObjectBinaryPackage.CharacterObject_battleEquipmentTemplate.SetValue(CharacterObject, characterMembers[0]);
            CharacterObjectBinaryPackage.CharacterObject_civilianEquipmentTemplate.SetValue(CharacterObject, characterMembers[1]);
            CharacterObjectBinaryPackage.CharacterObject_originCharacter.SetValue(CharacterObject, characterMembers[2]);
            CharacterObjectBinaryPackage.CharacterObject_UpgradeTargets.SetValue(CharacterObject, characterMembers);

            var factory = container.Resolve<IBinaryPackageFactory>();

            byte[] bytes = BinaryFormatterSerializer.Serialize(factory.GetBinaryPackage(CharacterObject));

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterObjectBinaryPackage>(obj);

            CharacterObjectBinaryPackage returnedPackage = (CharacterObjectBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CharacterObject newCharacterObject = returnedPackage.Unpack<CharacterObject>(deserializeFactory);

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
            // Setup
            CharacterObject characterObject = new CharacterObject();

            // Register object with new string id
            var objectManager = container.Resolve<IObjectManager>();
            Assert.True(objectManager.AddNewObject(characterObject, out string newId));

            characterObject.StringId = newId;

            BasicCharacterObject_basicName.SetValue(characterObject, new TextObject("Test Name"));

            var factory = container.Resolve<IBinaryPackageFactory>();
            CharacterObjectBinaryPackage package = new CharacterObjectBinaryPackage(characterObject, factory);

            // Execution
            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            // Verification
            Assert.IsType<CharacterObjectBinaryPackage>(obj);

            CharacterObjectBinaryPackage returnedPackage = (CharacterObjectBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CharacterObject newCharacterObject = returnedPackage.Unpack<CharacterObject>(deserializeFactory);

            Assert.Same(characterObject, newCharacterObject);
        }
    }
}
