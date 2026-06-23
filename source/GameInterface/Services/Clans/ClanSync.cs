using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

internal class ClanSync : IAutoSync
{
    public ClanSync(AutoSyncRegistry AutoSyncRegistry)
    {
        // Fields
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._isEliminated)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._kingdom)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._influence)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._midSettlement)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._basicTroop)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._leader)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._banner)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._tier)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._aggressiveness)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._tributeWallet)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._home)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Clan), nameof(Clan._clanDebtToKingdom)));

        // Properties
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Name)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.InformalName)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Culture)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.LastFactionChangeTime)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.AutoRecruitmentExpenses)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsNoble)));
        // CurrentTotalStrength is excluded: KingdomManager.HourlyTickClan calls UpdateCurrentStrength() on every
        // clan which zeroes then rebuilds the value with += per war party. Each assignment fires the AutoSync
        // setter, generating 250+ messages/sec. This is a derived/cached value recomputed server-side every hour;
        // clients have no AI or election logic that consumes it and do not need live sync.
        //AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.CurrentTotalStrength)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.MercenaryAwardMultiplier)));
        //AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.LabelColor)));
        //AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.InitialPosition)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsRebelClan)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.IsUnderMercenaryService)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Color)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.Color2)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerBackgroundColorPrimary)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerBackgroundColorSecondary)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.BannerIconColor)));
        // Clan.Renown is intentionally not AutoSynced here: its trivial auto-property setter gets JIT-inlined into
        // its writers (Clan.AddRenown / ResetClanRenown), so a setter prefix never fires. Renown is replicated
        // from those writer methods instead (ClanRenownPatch + ClanRenownHandler).
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Clan), nameof(Clan.NotAttackableByPlayerUntilTime)));
    }
}
