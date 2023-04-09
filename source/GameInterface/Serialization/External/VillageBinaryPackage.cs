using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class VillageBinaryPackage : BinaryPackageBase<Village>
    {
        public string StringId;

        public VillageBinaryPackage(Village obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = ResolveId<Village>(StringId);
        }
    }
}
