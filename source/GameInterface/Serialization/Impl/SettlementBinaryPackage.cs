using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class SettlementBinaryPackage : BinaryPackageBase<Settlement>
    {
        public string StringId;

        public SettlementBinaryPackage(Settlement obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<Settlement>(StringId);
        }
    }
}
