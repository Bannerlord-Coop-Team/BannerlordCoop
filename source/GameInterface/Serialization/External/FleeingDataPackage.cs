using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for FleeingDataPackage
    /// </summary>
    [Serializable]
    public class FleeingDataPackage : BinaryPackageBase<MobilePartyAi.FleeingData>
    {
        public FleeingDataPackage(MobilePartyAi.FleeingData obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal() => PackFields();

        protected override void UnpackInternal() => UnpackFields();
    }
}
