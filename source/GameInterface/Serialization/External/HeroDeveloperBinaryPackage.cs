using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for HeroDeveloper
    /// </summary>
    [Serializable]
    public class HeroDeveloperBinaryPackage : BinaryPackageBase<HeroDeveloper>
    {
        public HeroDeveloperBinaryPackage(HeroDeveloper obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
