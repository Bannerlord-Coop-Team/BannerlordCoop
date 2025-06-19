using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms
{
    internal class KingdomSync : IDynamicSync
    {
        public KingdomSync(DynamicSyncRegistry autoSyncBuilder) 
        {
            // Props
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.AlternativeColor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.AlternativeColor2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Banner)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Color)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Color2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Culture)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.EncyclopediaRulerTitle)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.EncyclopediaText)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.EncyclopediaTitle)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.InformalName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.InitialHomeLand)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.LabelColor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.LastArmyCreationDay)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.LastKingdomDecisionConclusionDate)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.LastMercenaryOfferTime)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.MainHeroCrimeRating)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.MercenaryWallet)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Name)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.NotAttackableByPlayerUntilTime)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.PrimaryBannerColor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.SecondaryBannerColor)));

			// Fields
			autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom.PoliticalStagnation)));
			autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._aggressiveness)));
			autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._isEliminated)));
			autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._kingdomBudgetWallet)));
			autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._kingdomMidSettlement)));
			autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._rulingClan)));
			autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._tributeWallet)));
			autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._distanceToClosestNonAllyFortificationCacheDirty)));
		}
    }
}
