using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CampaignTimeBinaryPackage : BinaryPackageBase<CampaignTime>
    {
        public CampaignTimeBinaryPackage(CampaignTime obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
