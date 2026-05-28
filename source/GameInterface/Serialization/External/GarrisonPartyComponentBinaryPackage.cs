using System;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for GarrisonPartyComponent
    /// </summary>
    [Serializable]
    public class GarrisonPartyComponentBinaryPackage : BinaryPackageBase<GarrisonPartyComponent>
    {
        public string MonsterId;

        public GarrisonPartyComponentBinaryPackage(GarrisonPartyComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
