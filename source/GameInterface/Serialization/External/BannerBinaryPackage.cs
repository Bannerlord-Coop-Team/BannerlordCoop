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
            // A joining player's clan banner arrives here inside the packed main hero. A clan banner with no
            // layers breaks every load, so an empty one gets the default here too, unlike network messages.
            Object = string.IsNullOrWhiteSpace(bannerData)
                ? string.Empty.ToSeededDefaultBanner()
                : bannerData.ToBannerWithLayers();
        }
    }
}
