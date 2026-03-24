using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for MobileParty
    /// </summary>
    [Serializable]
    public class MobilePartyBinaryPackage : BinaryPackageBase<MobileParty>
    {

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
            "_cachedPartySizeRatio",
            "<VersionNo>k__BackingField",
            "_lastNavigationFace",
            "_locatorNodeIndex",
            "<Scout>k__BackingField",
            "<Engineer>k__BackingField",
            "<Quartermaster>k__BackingField",
            "<Surgeon>k__BackingField",
            "_targetParty",
            "_targetSettlement",
            "<Anchor>k__BackingField",
            "Path",
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

            stringId = ResolveId(Object);

            base.PackFields(excludes);

            scoutId = ResolveId(Object.EffectiveScout);
            engineerId = ResolveId(Object.EffectiveEngineer);
            quartermasterId = ResolveId(Object.EffectiveQuartermaster);
            surgeonId = ResolveId(Object.EffectiveSurgeon);
        }

        private static ConstructorInfo MobileParty_ctor => typeof(MobileParty).GetConstructor(Array.Empty<Type>());
        protected override void UnpackInternal()
        {
            var resolvedObj = ResolveObject<MobileParty>(stringId);
            if (resolvedObj != null)
            {
                Object = resolvedObj;
                return;
            }

            MobileParty_ctor.Invoke(Object, Array.Empty<object>());

            base.UnpackFields();

            Object.Scout            = ResolveObject<Hero>(scoutId);
            Object.Engineer         = ResolveObject<Hero>(engineerId);
            Object.Quartermaster    = ResolveObject<Hero>(quartermasterId);
            Object.Surgeon          = ResolveObject<Hero>(surgeonId);


            Object.OnFinishLoadState();
        }
    }
}