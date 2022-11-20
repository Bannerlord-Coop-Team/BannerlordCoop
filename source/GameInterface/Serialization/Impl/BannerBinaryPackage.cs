using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class BannerBinaryPackage : BinaryPackageBase<Banner>
    {
        string bannerData;

        public BannerBinaryPackage(Banner obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            bannerData = Object.Serialize();
        }

        protected override void UnpackInternal()
        {
            Object = new Banner(bannerData);
        }
    }
}
