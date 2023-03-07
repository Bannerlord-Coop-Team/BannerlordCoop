using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for MobileParty
    /// </summary>
    [Serializable]
    public class MobilePartyBinaryPackage : BinaryPackageBase<MobileParty>
    {
        public static readonly PropertyInfo MobileParty_Scout = typeof(MobileParty).GetProperty("Scout", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly PropertyInfo MobileParty_Engineer = typeof(MobileParty).GetProperty("Engineer", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly PropertyInfo MobileParty_Quartermaster = typeof(MobileParty).GetProperty("Quartermaster", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly PropertyInfo MobileParty_Surgeon = typeof(MobileParty).GetProperty("Surgeon", BindingFlags.NonPublic | BindingFlags.Instance);

        private string stringId = string.Empty;

        private string scoutId;
        private string engineerId;
        private string quartermasterId;
        private string surgeonId;

        public MobilePartyBinaryPackage(MobileParty obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "<TaleWorlds.CampaignSystem.Map.ILocatable<TaleWorlds.CampaignSystem.Party.MobileParty>.NextLocatable>k__BackingField",
            "_partySizeRatioLastCheckVersion",
            "_itemRosterVersionNo",
            "_partyLastCheckAtNight",
            "_lastCalculatedBaseSpeedExplained",
            "_partyLastCheckIsPrisoner",
            "_lastCalculatedSpeed",
            "_partyPureSpeedLastCheckVersion",
            "_lastTargetedParties",
            "_partiesAroundPosition",
            "_aiPathLastFace",
            "_moveTargetAiFaceIndex",
            "_targetAiFaceIndex",
            "_cachedPartySizeLimit",
            "_cachedPartySizeRatio",
            "_latestUsedPaymentRatio",
            "<VersionNo>k__BackingField",
            "<Path>k__BackingField",
            "<PathBegin>k__BackingField",
            "<ForceAiNoPathMode>k__BackingField",
            "_errorPosition",
            "_currentNavigationFace",
            "_lastNavigationFace",
            "_locatorNodeIndex",
            "<Scout>k__BackingField",
            "<Engineer>k__BackingField",
            "<Quartermaster>k__BackingField",
            "<Surgeon>k__BackingField",
            "<MoveTargetParty>k__BackingField",
            "<MoveTargetPoint>k__BackingField",
            "_targetParty",
            "_targetSettlement",
            "_aiBehaviorMapEntity",
            // These are ignored as there is no way to resolve if
            // they already exist
            "_besiegerCamp",
            "_army",

        };

        protected override void PackInternal()
        {
            if (Object.Army != null) throw new Exception($"{nameof(Army)} is not handled in {nameof(MobilePartyBinaryPackage)}");
            if (Object.BesiegerCamp != null) throw new Exception($"{nameof(BesiegerCamp)} is not handled in {nameof(MobilePartyBinaryPackage)}");

            stringId = Object.StringId ?? string.Empty;

            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(excludes))
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }

            scoutId = Object.EffectiveScout?.StringId;
            engineerId = Object.EffectiveEngineer?.StringId;
            quartermasterId = Object.EffectiveQuartermaster?.StringId;
            surgeonId = Object.EffectiveSurgeon?.StringId;
        }


        private static readonly MethodInfo MobileParty_InitCached = typeof(MobileParty).GetMethod("InitCached", BindingFlags.NonPublic | BindingFlags.Instance);
        protected override void UnpackInternal()
        {
            MobileParty mobileParty = MBObjectManager.Instance.GetObject<MobileParty>(stringId);
            if(mobileParty != null)
            {
                Object = mobileParty;
                return;
            }

            MobileParty_InitCached.Invoke(Object, new object[0]);

            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            MobileParty_Scout        .SetValue(Object, ResolveId<Hero>(scoutId));
            MobileParty_Engineer     .SetValue(Object, ResolveId<Hero>(engineerId));
            MobileParty_Quartermaster.SetValue(Object, ResolveId<Hero>(quartermasterId));
            MobileParty_Surgeon      .SetValue(Object, ResolveId<Hero>(surgeonId));
        }
    }
}