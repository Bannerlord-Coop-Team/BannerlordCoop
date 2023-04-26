using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CharacterTraitsBinaryPackage : BinaryPackageBase<CharacterTraits>
    {
        public CharacterTraitsBinaryPackage(CharacterTraits obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
