using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CultureObjectBinaryPackage : BinaryPackageBase<CultureObject>
    {
        public string StringId;

        public CultureObjectBinaryPackage(CultureObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<CultureObject>(StringId);
        }
    }
}
