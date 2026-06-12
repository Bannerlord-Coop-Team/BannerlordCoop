using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

internal class ClanSync : IDynamicSync
{
    public ClanSync(DynamicSyncRegistry dynamicSyncRegistry)
    {
        // Fields
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._isEliminated)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._kingdom)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._influence)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._midSettlement)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._basicTroop)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._leader)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._banner)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._tier)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._aggressiveness)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._tributeWallet)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._home)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._clanDebtToKingdom)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._supporterNotablesCache)));

        // Properties
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Name)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.InformalName)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Culture)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.LastFactionChangeTime)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.AutoRecruitmentExpenses)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsNoble)));
        // CurrentTotalStrength is excluded: KingdomManager.HourlyTickClan calls UpdateCurrentStrength() on every
        // clan which zeroes then rebuilds the value with += per war party. Each assignment fires the DynamicSync
        // setter, generating 250+ messages/sec. This is a derived/cached value recomputed server-side every hour;
        // clients have no AI or election logic that consumes it and do not need live sync.
        //dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.CurrentTotalStrength)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.MercenaryAwardMultiplier)));
        //dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.LabelColor)));
        //dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.InitialPosition)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsRebelClan)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsUnderMercenaryService)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Color)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Color2)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerBackgroundColorPrimary)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerBackgroundColorSecondary)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerIconColor)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Renown)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.NotAttackableByPlayerUntilTime)));
    }
}
