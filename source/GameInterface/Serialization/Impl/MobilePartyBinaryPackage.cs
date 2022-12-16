using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for MobileParty
    /// </summary>
    [Serializable]
    public class MobilePartyBinaryPackage : BinaryPackageBase<MobileParty>
    {
        public MobilePartyBinaryPackage(MobileParty obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "ILocatable<MobileParty>.NextLocatable",
            "_partySizeRatioLastCheckVersion",
            "_itemRosterVersionNo",
            "_partyLastCheckAtNight",
            "_lastCalculatedBaseSpeedExplained",
            "_partyLastCheckIsPrisoner",
            "_lastCalculatedSpeed",
            "_partyPureSpeedLastCheckVersion",
            "_attachedParties",
            "_lastTargetedParties",
            "_partiesAroundPosition",
            "_aiPathLastFace",
            "_moveTargetAiFaceIndex",
            "_targetAiFaceIndex",
            "_isDisorganized",
            "_cachedPartySizeLimit",
            "_cachedPartySizeRatio",
            "_latestUsedPaymentRatio",
            "IsBandit",
            "IsCustomParty",
            "IsCommonAreaParty",
            "IsGarrison",
            "IsCaravan",
            "IsVillager",
            "IsLordParty",
            "IsMilitia",
            "CurrentNavigationFace",
            "VersionNo",
            "Path",
            "PathBegin",
            "ForceAiNoPathMode",
            "AiBehaviorPartyBase",
            "_errorPosition",
            "_currentNavigationFace",
            "_lastNavigationFace",
            "_locatorNodeIndex",
        };

        public override void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }
        }
    }
}