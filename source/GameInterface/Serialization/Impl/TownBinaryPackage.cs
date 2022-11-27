using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class TownBinaryPackage : BinaryPackageBase<Town>
    {
        public string StringId;

        public TownBinaryPackage(Town obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<Town>(StringId);
        }
    }
}
