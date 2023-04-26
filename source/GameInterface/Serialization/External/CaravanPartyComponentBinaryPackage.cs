using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
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
            base.PackInternal(excludes);
        }
    }
}
