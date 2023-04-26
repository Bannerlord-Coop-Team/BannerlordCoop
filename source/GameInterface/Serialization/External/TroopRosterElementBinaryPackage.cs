using System;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for TroopRosterElement
    /// </summary>
    [Serializable]
    public class TroopRosterElementBinaryPackage : BinaryPackageBase<TroopRosterElement>
    {
        public TroopRosterElementBinaryPackage(TroopRosterElement obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
