using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;
using Common.Extensions;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterTraitsSerializationTest
    {
        [Fact]
        public void CharacterTraits_Serialize()
        {
            CharacterTraits testCharacterTraits = (CharacterTraits)FormatterServices.GetUninitializedObject(typeof(CharacterTraits));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterTraitsBinaryPackage package = new CharacterTraitsBinaryPackage(testCharacterTraits, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CharacterTraits_Full_Serialization()
        {
            CharacterTraits testCharacterTraits = (CharacterTraits)FormatterServices.GetUninitializedObject(typeof(CharacterTraits));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterTraitsBinaryPackage package = new CharacterTraitsBinaryPackage(testCharacterTraits, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterTraitsBinaryPackage>(obj);

            CharacterTraitsBinaryPackage returnedPackage = (CharacterTraitsBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
