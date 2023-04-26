using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for BannerComponent
    /// </summary>
    [Serializable]
    public class BannerComponentBinaryPackage : BinaryPackageBase<BannerComponent>
    {
        public BannerComponentBinaryPackage(BannerComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
        
        protected override void PackInternal()
        {
            base.PackFields();
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();
        }
    }
}
