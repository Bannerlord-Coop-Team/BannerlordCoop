using GameInterface.Services.Banners.Extensions;
using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BannerBinaryPackage : BinaryPackageBase<Banner>
    {
        string bannerData;

        public BannerBinaryPackage(Banner obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            bannerData = Object.Serialize();
        }

        protected override void UnpackInternal()
        {
            // A joining player's clan banner arrives here inside the packed main hero.
            Object = bannerData.ToBannerWithLayers();
        }
    }
}
