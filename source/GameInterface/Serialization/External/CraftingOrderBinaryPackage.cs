using System;
using TaleWorlds.CampaignSystem.CraftingSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CraftingOrderBinaryPackage : BinaryPackageBase<CraftingOrder>
    {
        public CraftingOrderBinaryPackage(CraftingOrder obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
