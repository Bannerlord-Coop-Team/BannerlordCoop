using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for MobileParty
    /// </summary>
    [Serializable]
    public class MobilePartyBinaryPackage : BinaryPackageBase<MobileParty>
    {
        public MobilePartyBinaryPackage(MobileParty obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            // TODO implement
        }

        protected override void UnpackInternal()
        {
            // TODO implement
        }
    }
}
