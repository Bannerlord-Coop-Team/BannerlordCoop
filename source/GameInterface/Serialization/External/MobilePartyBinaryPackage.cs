﻿using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for MobileParty
    /// </summary>
    [Serializable]
    public class MobilePartyBinaryPackage : BinaryPackageBase<MobileParty>
    {
        public static PropertyInfo MobileParty_Scout => typeof(MobileParty).GetProperty("Scout", BindingFlags.NonPublic | BindingFlags.Instance);
        public static PropertyInfo MobileParty_Engineer => typeof(MobileParty).GetProperty("Engineer", BindingFlags.NonPublic | BindingFlags.Instance);
        public static PropertyInfo MobileParty_Quartermaster => typeof(MobileParty).GetProperty("Quartermaster", BindingFlags.NonPublic | BindingFlags.Instance);
        public static PropertyInfo MobileParty_Surgeon => typeof(MobileParty).GetProperty("Surgeon", BindingFlags.NonPublic | BindingFlags.Instance);
        public static MethodInfo MobileParty_OnFinishLoadState => typeof(MobileParty).GetMethod("OnFinishLoadState", BindingFlags.Instance | BindingFlags.NonPublic);

        private string stringId = string.Empty;

        private string scoutId;
        private string engineerId;
        private string quartermasterId;
        private string surgeonId;

        public MobilePartyBinaryPackage(MobileParty obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
            "_cachedPartySizeLimit",
            "_cachedPartySizeRatio",
            "_latestUsedPaymentRatio",
            "<VersionNo>k__BackingField",
            "_currentNavigationFace",
            "_lastNavigationFace",
            "_locatorNodeIndex",
            "<Scout>k__BackingField",
            "<Engineer>k__BackingField",
            "<Quartermaster>k__BackingField",
            "<Surgeon>k__BackingField",
            "_targetParty",
            "_targetSettlement",
            // These are ignored as there is no way to resolve if
            // they already exist
            "_besiegerCamp",
            "_army",
            "<ThinkParamsCache>k__BackingField",
        };

        protected override void PackInternal()
        {
            if (Object.Army != null) throw new Exception($"{nameof(Army)} is not handled in {nameof(MobilePartyBinaryPackage)}");
            if (Object.BesiegerCamp != null) throw new Exception($"{nameof(BesiegerCamp)} is not handled in {nameof(MobilePartyBinaryPackage)}");

            stringId = Object.StringId ?? string.Empty;

            base.PackFields(excludes);

            scoutId = Object.EffectiveScout?.StringId;
            engineerId = Object.EffectiveEngineer?.StringId;
            quartermasterId = Object.EffectiveQuartermaster?.StringId;
            surgeonId = Object.EffectiveSurgeon?.StringId;
        }

        private static ConstructorInfo MobileParty_ctor => typeof(MobileParty).GetConstructor(Array.Empty<Type>());
        protected override void UnpackInternal()
        {
            if(string.IsNullOrEmpty(stringId) == false)
            {
                Object = ResolveId<MobileParty>(stringId);
                return;
            }

            MobileParty_ctor.Invoke(Object, Array.Empty<object>());

            base.UnpackFields();

            MobileParty_Scout        .SetValue(Object, ResolveId<Hero>(scoutId));
            MobileParty_Engineer     .SetValue(Object, ResolveId<Hero>(engineerId));
            MobileParty_Quartermaster.SetValue(Object, ResolveId<Hero>(quartermasterId));
            MobileParty_Surgeon      .SetValue(Object, ResolveId<Hero>(surgeonId));


            MobileParty_OnFinishLoadState.Invoke(Object, Array.Empty<string>());
        }
    }
}