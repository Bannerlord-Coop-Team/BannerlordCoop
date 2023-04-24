using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class MBCharacterSkillsBinaryPackage : BinaryPackageBase<MBCharacterSkills>
    {
        public MBCharacterSkillsBinaryPackage(MBCharacterSkills obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
