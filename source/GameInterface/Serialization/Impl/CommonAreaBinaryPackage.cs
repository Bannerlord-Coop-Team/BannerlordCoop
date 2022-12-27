using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for CommonArea
    /// </summary>
    [Serializable]
    public class CommonAreaBinaryPackage : BinaryPackageBase<CommonArea>
    {
        private string settlementStringId;
        private int commonAreaIndex;

        public CommonAreaBinaryPackage(CommonArea obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            settlementStringId = Object.Settlement.StringId;

            // CommonArea is generated once per campaign so we can resolve it by
            // Using the index of that common area in it's settlement
            commonAreaIndex = Object.Settlement.CommonAreas.FindIndex(i => i == Object);
        }

        protected override void UnpackInternal()
        {
            Settlement settlement = MBObjectManager.Instance.GetObject<Settlement>(settlementStringId);

            // CommonArea is generated once per campaign so we can resolve it by
            // Using the index of that common area in it's settlement
            Object = settlement.CommonAreas[commonAreaIndex];
        }
    }
}
