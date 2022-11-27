using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class CharacterTraitsBinaryPackage : BinaryPackageBase<CharacterTraits>
    {
        public string StringId;

        public CharacterTraitsBinaryPackage(CharacterTraits obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        public override void Pack()
        {
            StringId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<CharacterTraits>(StringId);
        }
    }
}
