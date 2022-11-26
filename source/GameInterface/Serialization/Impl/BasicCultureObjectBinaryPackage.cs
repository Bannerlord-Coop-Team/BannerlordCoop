using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class BasicCultureObjectBinaryPackage : BinaryPackageBase<BasicCultureObject>
    {
        public string stringId;

        public BasicCultureObjectBinaryPackage(BasicCultureObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            stringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<BasicCultureObject>(stringId);
        }
    }
}
