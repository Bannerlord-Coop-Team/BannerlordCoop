using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class SkillObjectBinaryPackage : BinaryPackageBase<SkillObject>
    {
        public string StringId;
        public SkillObjectBinaryPackage(SkillObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            MBObjectManager.Instance?.GetObject<SkillObject>(StringId);
        }
    }
}
