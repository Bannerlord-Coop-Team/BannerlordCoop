using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class PolicyObjectBinaryPackage : BinaryPackageBase<PolicyObject>
    {
        public string StringId;
        public PolicyObjectBinaryPackage(PolicyObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            MBObjectManager.Instance.GetObject<PolicyObject>(StringId);
        }
    }
}
