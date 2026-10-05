using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for <see cref="PartyConfiguration"/>, the per-leader party commands v1.5 added to
    /// <c>Hero._partyConfiguration</c>. Hero packages pack every field, so heroes carrying one need this.
    /// </summary>
    [Serializable]
    public class PartyConfigurationBinaryPackage : BinaryPackageBase<PartyConfiguration>
    {
        public PartyConfigurationBinaryPackage(PartyConfiguration obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
