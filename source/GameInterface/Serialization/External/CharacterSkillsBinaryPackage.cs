using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CharacterSkillsBinaryPackage : BinaryPackageBase<CharacterSkills>
    {
        public CharacterSkillsBinaryPackage(CharacterSkills obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
