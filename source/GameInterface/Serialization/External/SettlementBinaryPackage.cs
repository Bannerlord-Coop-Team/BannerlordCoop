using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class SettlementBinaryPackage : BinaryPackageBase<Settlement>
    {
        public string StringId;

        public SettlementBinaryPackage(Settlement obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<Settlement>(StringId);
        }
    }
}
