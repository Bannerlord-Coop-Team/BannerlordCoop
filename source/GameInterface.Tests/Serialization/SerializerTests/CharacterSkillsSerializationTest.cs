using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            CharacterSkills CharacterSkills = new CharacterSkills();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterSkillsBinaryPackage package = new CharacterSkillsBinaryPackage(CharacterSkills, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _attributes = typeof(PropertyOwner<SkillObject>).GetField("_attributes", BindingFlags.NonPublic | BindingFlags.Instance);
        [Fact]
        public void CharacterSkills_Full_Serialization()
        {
            CharacterSkills CharacterSkills = new CharacterSkills();

            Dictionary<SkillObject, int> skills = new Dictionary<SkillObject, int>
            {
                { new SkillObject("MySkill"), 5 },
                { new SkillObject("MySkill2"), 6 },
            };

            _attributes.SetValue(CharacterSkills, skills);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CharacterSkillsBinaryPackage package = new CharacterSkillsBinaryPackage(CharacterSkills, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CharacterSkillsBinaryPackage>(obj);

            CharacterSkillsBinaryPackage returnedPackage = (CharacterSkillsBinaryPackage)obj;

            CharacterSkills newCharacterSkills = returnedPackage.Unpack<CharacterSkills>();

            Assert.Equal(CharacterSkills.StringId, CharacterSkills.StringId);
            Assert.Equal(CharacterSkills.Id, CharacterSkills.Id);
            Assert.Equal(CharacterSkills.IsReady, newCharacterSkills.IsReady);

            Dictionary<SkillObject, int> newSkills = (Dictionary<SkillObject, int>)_attributes.GetValue(newCharacterSkills);

            Assert.Equal(skills.ToString(), newSkills.ToString());
        }
    }
}
