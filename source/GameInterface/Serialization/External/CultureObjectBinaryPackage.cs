using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CultureObjectBinaryPackage : BinaryPackageBase<CultureObject>
    {
        public string StringId;

        public CultureObjectBinaryPackage(CultureObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = ResolveId<CultureObject>(StringId);
        }
    }
}
