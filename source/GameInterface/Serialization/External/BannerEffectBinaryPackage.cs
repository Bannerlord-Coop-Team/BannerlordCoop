using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class BannerEffectBinaryPackage : BinaryPackageBase<BannerEffect>
    {
        public string stringId;

        public BannerEffectBinaryPackage(BannerEffect obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            stringId = Object.StringId;
        }

        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<BannerEffect>(stringId);
        }
    }
}
