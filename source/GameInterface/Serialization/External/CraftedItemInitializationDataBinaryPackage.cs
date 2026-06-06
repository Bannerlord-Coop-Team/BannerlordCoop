using System;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CraftedItemInitializationDataBinaryPackage : BinaryPackageBase<CraftedItemInitializationData>
    {
        public CraftedItemInitializationDataBinaryPackage(CraftedItemInitializationData obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
