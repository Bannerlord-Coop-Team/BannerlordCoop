using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class PerkObjectBinaryPackage : BinaryPackageBase<PerkObject>
    {
        public string StringId;

        public PerkObjectBinaryPackage(PerkObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }

        protected override void UnpackInternal()
        {
            Object = ResolveId<PerkObject>(StringId);
        }
    }
}
