using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for PartyBase
    /// </summary>
    [Serializable]
    public class PartyBaseBinaryPackage : BinaryPackageBase<PartyBase>
    {
        public PartyBaseBinaryPackage(PartyBase obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
