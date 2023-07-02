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
            "<MoveTargetParty>k__BackingField",
            "<Path>k__BackingField",
            "<ForceAiNoPathMode>k__BackingField",
            "<AiBehaviorPartyBase>k__BackingField",
            "_targetAiFaceIndex",
            "_moveTargetAiFaceIndex",
            "_aiPathLastFace",
            "_lastTargetedParties",
            "_aiBehaviorMapEntity",
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);
        }

        private static readonly FieldInfo MobilePartyAi_lastTargetedParties = typeof(MobilePartyAi).GetField("_lastTargetedParties", BindingFlags.NonPublic | BindingFlags.Instance);
        protected override void UnpackInternal()
        {
            base.UnpackFields();

            MobilePartyAi_lastTargetedParties.SetValue(Object, new List<MobileParty>());
        }
    }
}
