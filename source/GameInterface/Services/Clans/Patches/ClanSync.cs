using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Patches
{
    internal class ClanSync : IAutoSync
    {
        public ClanSync(IAutoSyncBuilder autoSyncBuilder) 
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Name)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.InformalName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Culture)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.LastFactionChangeTime)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.AutoRecruitmentExpenses)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsNoble)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.TotalStrength)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.MercenaryAwardMultiplier)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.LabelColor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.InitialPosition)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsRebelClan)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsUnderMercenaryService)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Color)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Color2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerBackgroundColorPrimary)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerBackgroundColorSecondary)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerIconColor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan._midPointCalculated)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Renown)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.NotAttackableByPlayerUntilTime)));


        }
    }
}
