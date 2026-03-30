using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for CampaignVec2
    /// </summary>
    [Serializable]
    public class CampaignVec2BinaryPackage : BinaryPackageBase<CampaignVec2>
    {
        public CampaignVec2BinaryPackage(CampaignVec2 obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
