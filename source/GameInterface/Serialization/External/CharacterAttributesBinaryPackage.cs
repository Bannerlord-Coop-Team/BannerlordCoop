using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CharacterAttributesBinaryPackage : BinaryPackageBase<CharacterAttributes>
    {
        public CharacterAttributesBinaryPackage(CharacterAttributes obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
