using System;
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

            Object.Scout            = ResolveId<Hero>(scoutId);
            Object.Engineer         = ResolveId<Hero>(engineerId);
            Object.Quartermaster    = ResolveId<Hero>(quartermasterId);
            Object.Surgeon          = ResolveId<Hero>(surgeonId);


            Object.OnFinishLoadState();
        }
    }
}