using GameInterface.Services.Modules;
using System;


namespace GameInterface.Serialization.Internal
{
    /// <summary>
    /// Binary package for CompatibilityInfo
    /// </summary>
    /// 
    [Serializable]
    public class CompatibilityInfoBinaryPackage : BinaryPackageBase<CompatibilityInfo>
    {
        public CompatibilityInfoBinaryPackage(CompatibilityInfo obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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