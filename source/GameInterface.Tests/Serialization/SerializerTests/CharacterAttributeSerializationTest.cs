using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterAttributeSerializationTest
    {
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
            CharacterAttribute testCharacterAttribute = new CharacterAttribute("test");

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterAttributeBinaryPackage package = new CharacterAttributeBinaryPackage(testCharacterAttribute, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterAttributeBinaryPackage>(obj);

            CharacterAttributeBinaryPackage returnedPackage = (CharacterAttributeBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
