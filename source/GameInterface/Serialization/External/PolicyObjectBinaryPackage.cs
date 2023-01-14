using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class PolicyObjectBinaryPackage : BinaryPackageBase<PolicyObject>
    {
        public string StringId;
        public PolicyObjectBinaryPackage(PolicyObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            MBObjectManager.Instance.GetObject<PolicyObject>(StringId);
        }
    }
}
