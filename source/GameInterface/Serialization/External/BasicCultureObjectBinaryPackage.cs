using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BasicCultureObjectBinaryPackage : BinaryPackageBase<BasicCultureObject>
    {
        public string stringId;

        public BasicCultureObjectBinaryPackage(BasicCultureObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            stringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<BasicCultureObject>(stringId);
        }
    }
}
