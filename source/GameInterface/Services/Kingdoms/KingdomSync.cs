using System;
using System.Collections.Generic;
using System.Text;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms
{
    internal class KingdomSync : IAutoSync
    {
        public KingdomSync(IAutoSyncBuilder autoSyncBuilder) 
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.AlternativeColor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.AlternativeColor2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Banner)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Color)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Color2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.Culture)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Kingdom), nameof(Kingdom.AlternativeColor)));
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
        }
    }
}
