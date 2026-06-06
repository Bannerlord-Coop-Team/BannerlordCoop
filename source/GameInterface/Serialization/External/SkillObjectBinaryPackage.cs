using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class SkillObjectBinaryPackage : BinaryPackageBase<SkillObject>
    {
        public string StringId;
        public SkillObjectBinaryPackage(SkillObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = ResolveId(Object);
        }
        protected override void UnpackInternal()
        {
            Object = ResolveObject<SkillObject>(StringId);
        }
    }
}
