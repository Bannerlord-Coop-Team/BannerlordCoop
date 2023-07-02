using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for CaravanPartyComponent
    /// </summary>
    [Serializable]
    public class CaravanPartyComponentBinaryPackage : BinaryPackageBase<CaravanPartyComponent>
    {
        public CaravanPartyComponentBinaryPackage(CaravanPartyComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "_cachedName",
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);
        }
        
        protected override void UnpackInternal()
        {
            base.UnpackFields();
        }
    }
}
