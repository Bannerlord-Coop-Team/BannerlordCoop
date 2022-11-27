using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class VillageBinaryPackage : BinaryPackageBase<Village>
    {
        public string StringId;

        public VillageBinaryPackage(Village obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<Village>(StringId);
        }
    }
}
