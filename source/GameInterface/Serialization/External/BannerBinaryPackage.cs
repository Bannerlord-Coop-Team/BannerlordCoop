using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BannerBinaryPackage : BinaryPackageBase<Banner>
    {
        string bannerData;

        public BannerBinaryPackage(Banner obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            bannerData = Object.Serialize();
        }

        protected override void UnpackInternal()
        {
            Object = new Banner(bannerData);
        }
    }
}
