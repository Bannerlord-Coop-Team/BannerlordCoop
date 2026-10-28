using Common.Logging;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BannerBinaryPackage : BinaryPackageBase<Banner>
    {
        private static readonly ILogger Logger = LogManager.GetLogger<BannerBinaryPackage>();

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
            // A banner with no layers throws on the next colour change, so hand back no banner instead,
            // the same as a clan that never had one, which vanilla's colour updates already skip.
            if (string.IsNullOrEmpty(bannerData) ||
                !Banner.TryGetBannerDataFromCode(bannerData, out var layers) ||
                layers.Count == 0)
            {
                Logger.Warning("Unpacked a banner code with no readable layers ({BannerCode}), leaving the banner unset", bannerData);
                Object = null;
                return;
            }

            Object = new Banner(bannerData);
        }
    }
}
