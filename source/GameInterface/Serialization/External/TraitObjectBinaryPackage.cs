using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class TraitObjectBinaryPackage : BinaryPackageBase<TraitObject>
    {
        public string StringId;

        public TraitObjectBinaryPackage(TraitObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            StringId = ResolveId(Object);
        }
        protected override void UnpackInternal()
        {
            Object = ResolveObject<TraitObject>(StringId);
        }
    }
}
