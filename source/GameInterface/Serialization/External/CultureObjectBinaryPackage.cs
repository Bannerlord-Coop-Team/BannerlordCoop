using System;
using TaleWorlds.CampaignSystem;

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
            StringId = ResolveId(Object);
        }
        protected override void UnpackInternal()
        {
            Object = ResolveObject<CultureObject>(StringId);
        }
    }
}
