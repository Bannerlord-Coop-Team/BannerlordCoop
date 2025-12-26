using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for PartyAI
    /// </summary>
    [Serializable]
    public class MobilePartyAIBinaryPackage : BinaryPackageBase<MobilePartyAi>
    {
        public MobilePartyAIBinaryPackage(MobilePartyAi obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "<AiBehaviorPartyBase>k__BackingField",
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();

            //Object._lastTargetedParties = new List<MobileParty>();
            //Object.InitCached();
        }
    }
}
