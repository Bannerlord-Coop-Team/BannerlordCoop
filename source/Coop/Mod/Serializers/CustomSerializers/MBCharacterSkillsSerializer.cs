using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class MBCharacterSkillsSerializer : ICustomSerializer
    {
        CharacterSkillsSerializer characterSkillsSerializer;
        public MBCharacterSkillsSerializer(MBCharacterSkills value)
        {
            characterSkillsSerializer = new CharacterSkillsSerializer(value.Skills);
        }

        public object Deserialize()
        {
            MBCharacterSkills skills = MBObjectManager.Instance.CreateObject<MBCharacterSkills>();
            typeof(MBCharacterSkills).GetProperty("Skills")
                .SetValue(skills, characterSkillsSerializer.Deserialize(), BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
            return skills;
        }
    }
}