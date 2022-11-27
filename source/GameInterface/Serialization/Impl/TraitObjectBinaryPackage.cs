using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class TraitObjectBinaryPackage : BinaryPackageBase<TraitObject>
    {
        public string StringId;

        public TraitObjectBinaryPackage(TraitObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<TraitObject>(StringId);
        }
    }
}
