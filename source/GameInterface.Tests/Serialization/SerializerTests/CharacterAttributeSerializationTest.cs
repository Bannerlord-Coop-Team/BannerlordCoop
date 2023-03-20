using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterAttributeSerializationTest
    {
        public CharacterAttributeSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void CharacterAttribute_Serialize()
        {
            CharacterAttribute testCharacterAttribute = new CharacterAttribute("test");

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterAttributeBinaryPackage package = new CharacterAttributeBinaryPackage(testCharacterAttribute, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CharacterAttribute_Full_Serialization()
        {
            CharacterAttribute characterAttribute = new CharacterAttribute("test");

            MBObjectManager.Instance.RegisterObject(characterAttribute);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterAttributeBinaryPackage package = new CharacterAttributeBinaryPackage(characterAttribute, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterAttributeBinaryPackage>(obj);

            CharacterAttributeBinaryPackage returnedPackage = (CharacterAttributeBinaryPackage)obj;

            CharacterAttribute newCharacterAttribute = returnedPackage.Unpack<CharacterAttribute>();

            Assert.Same(characterAttribute, newCharacterAttribute);
        }
    }
}
