using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class CharacterSkillsSurrogate
    {
        public static implicit operator CharacterSkillsSurrogate(CharacterSkills obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator CharacterSkills(CharacterSkillsSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}