using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for CommonArea
    /// </summary>
    [Serializable]
    public class AlleyBinaryPackage : BinaryPackageBase<Alley>
    {
        private string settlementStringId;
        private int commonAreaIndex;

        public AlleyBinaryPackage(Alley obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            settlementStringId = Object.Settlement.StringId;

            // CommonArea is generated once per campaign so we can resolve it by
            // Using the index of that common area in it's settlement
            commonAreaIndex = Object.Settlement.Alleys.FindIndex(i => i == Object);
        }

        protected override void UnpackInternal()
        {
            Settlement settlement = MBObjectManager.Instance.GetObject<Settlement>(settlementStringId);

            // CommonArea is generated once per campaign so we can resolve it by
            // Using the index of that common area in it's settlement
            Object = settlement.Alleys[commonAreaIndex];
        }
    }
}
