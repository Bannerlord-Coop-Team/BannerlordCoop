using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for HeroDeveloper
    /// </summary>
    [Serializable]
    public class HeroDeveloperBinaryPackage : BinaryPackageBase<HeroDeveloper>
    {
        public HeroDeveloperBinaryPackage(HeroDeveloper obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
        
        protected override void PackInternal()
        {
            base.PackFields();
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();
        }
    }
}
