using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CharacterSkillsSerializationTest
    {
        IContainer container;
        public CharacterSkillsSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CharacterSkills_Serialize()
        {
            CharacterSkills CharacterSkills = new CharacterSkills();

            var factory = container.Resolve<IBinaryPackageFactory>();
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

            SkillObject skill1 = new SkillObject("MySkill1");
            SkillObject skill2 = new SkillObject("MySkill2");

            MBObjectManager.Instance.RegisterObject(skill1);
            MBObjectManager.Instance.RegisterObject(skill2);

            Dictionary<SkillObject, int> skills = new Dictionary<SkillObject, int>
            {
                { skill1, 5 },
                { skill2, 6 },
            };

            _attributes.SetValue(CharacterSkills, skills);

            var factory = container.Resolve<IBinaryPackageFactory>();
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
