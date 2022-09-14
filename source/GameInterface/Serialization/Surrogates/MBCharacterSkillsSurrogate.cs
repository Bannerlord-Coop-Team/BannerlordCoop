using ProtoBuf;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public class MBCharacterSkillsSurrogate
    {
        #region Reflection
        private static readonly PropertyInfo p_skills = typeof(MBCharacterSkills).GetProperty(nameof(MBCharacterSkills.Skills));
        #endregion

        [ProtoMember(1)]
        private CharacterSkills Skills;

        private MBCharacterSkillsSurrogate(MBCharacterSkills obj)
        {
            Skills = obj.Skills;
        }

        private MBCharacterSkills Deserialize()
        {
            MBCharacterSkills characterSkills = new MBCharacterSkills();

            p_skills.SetValue(characterSkills, Skills);

            return characterSkills;
        }

        public static implicit operator MBCharacterSkillsSurrogate(MBCharacterSkills obj)
        {
            if (obj == null) return null;

            return new MBCharacterSkillsSurrogate(obj);
        }

        public static implicit operator MBCharacterSkills(MBCharacterSkillsSurrogate surrogate)
        {
            if (surrogate == null) return null;

            return surrogate.Deserialize();
        }
    }
}
