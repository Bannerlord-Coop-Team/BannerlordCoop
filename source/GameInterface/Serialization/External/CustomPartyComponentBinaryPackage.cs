using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CustomPartyComponentBinaryPackage : BinaryPackageBase<CustomPartyComponent>
    {
        public CustomPartyComponentBinaryPackage(CustomPartyComponent obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        static readonly HashSet<string> excludes = new HashSet<string>
        {
            "_cachedName",
        };

        protected override void PackInternal()
        {
            base.PackInternal(excludes);
        }
    }
}
