using System;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for PatrolPartyComponent
    /// </summary>
    [Serializable]
    public class PatrolPartyComponentBinaryPackage : BinaryPackageBase<PatrolPartyComponent>
    {
        public string MonsterId;

        public PatrolPartyComponentBinaryPackage(PatrolPartyComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
