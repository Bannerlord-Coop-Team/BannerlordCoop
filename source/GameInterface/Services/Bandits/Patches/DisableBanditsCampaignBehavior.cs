using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(BanditSpawnCampaignBehavior))]
internal class DisableBanditsCampaignBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableBanditsCampaignBehavior>();

    [HarmonyPatch(nameof(BanditSpawnCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch("CacheBanditCounts")]
    [HarmonyPrefix]
    private static bool CacheBanditCountsPrefix(BanditSpawnCampaignBehavior __instance)
    {
        var banditCountsPerHideout = new Dictionary<Settlement, int>();
        foreach (var party in MobileParty.AllBanditParties)
        {
            var clan = party.ActualClan;
            if (clan == null ||
                (!__instance.IsBanditFaction(clan) &&
                 !BanditSpawnCampaignBehavior.IsLooterFaction(clan)))
            {
                continue;
            }

            var homeSettlement = party.HomeSettlement;
            if (homeSettlement == null)
            {
                Logger.Warning("Skipping bandit party {PartyId} with no home settlement while rebuilding bandit counts",
                    party.StringId);
                continue;
            }

            banditCountsPerHideout.TryGetValue(homeSettlement, out var value);
            banditCountsPerHideout[homeSettlement] = value + 1;
        }

        __instance._banditCountsPerHideout = banditCountsPerHideout;
        return false;
    }

    [HarmonyPatch("MobilePartyCreated")]
    [HarmonyPrefix]
    private static bool MobilePartyCreatedPrefix(MobileParty party)
    {
        return HasCacheableHomeSettlement(party);
    }

    [HarmonyPatch("MobilePartyDestroyed")]
    [HarmonyPrefix]
    private static bool MobilePartyDestroyedPrefix(MobileParty party)
    {
        return HasCacheableHomeSettlement(party);
    }

    internal static bool HasCacheableHomeSettlement(MobileParty party)
    {
        if (!party.IsBandit || party.ActualClan == null || party.HomeSettlement != null)
        {
            return true;
        }

        Logger.Warning("Skipping bandit count update for party {PartyId} with no home settlement",
            party.StringId);
        return false;
    }
}
