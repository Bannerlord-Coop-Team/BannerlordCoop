using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class SkillObjectBinaryPackage : BinaryPackageBase<SkillObject>
    {
        public string StringId;
        public SkillObjectBinaryPackage(SkillObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<SkillObject>(StringId);
        }
    }
}
