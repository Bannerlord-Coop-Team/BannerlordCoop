using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class PolicyObjectBinaryPackage : BinaryPackageBase<PolicyObject>
    {
        public string StringId;
        public PolicyObjectBinaryPackage(PolicyObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = ResolveId(Object);
        }
        protected override void UnpackInternal()
        {
            Object = ResolveObject<PolicyObject>(StringId);
        }
    }
}
