using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterSkillsSerializationTest
    {
        [Fact]
        public void CharacterSkills_Serialize()
        {
            CharacterSkills CharacterSkills = (CharacterSkills)FormatterServices.GetUninitializedObject(typeof(CharacterSkills));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterSkillsBinaryPackage package = new CharacterSkillsBinaryPackage(CharacterSkills, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CharacterSkills_Full_Serialization()
        {
            CharacterSkills CharacterSkills = (CharacterSkills)FormatterServices.GetUninitializedObject(typeof(CharacterSkills));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterSkillsBinaryPackage package = new CharacterSkillsBinaryPackage(CharacterSkills, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterSkillsBinaryPackage>(obj);

            CharacterSkillsBinaryPackage returnedPackage = (CharacterSkillsBinaryPackage)obj;

            CharacterSkills newCharacterSkills = returnedPackage.Unpack<CharacterSkills>();

            Assert.Equal(CharacterSkills.StringId, newCharacterSkills.StringId);
        }
    }
}
