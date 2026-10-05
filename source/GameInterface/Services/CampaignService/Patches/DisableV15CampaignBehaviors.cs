using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace GameInterface.Services.CampaignService.Patches;

/// <summary>
/// Campaign behaviors added in v1.5 that change synchronized campaign state. Like the other disabled
/// behaviors, they run on the server only; their results reach clients through the usual sync.
/// <list type="bullet">
/// <item>HeroDailyXpCampaignBehavior: daily skill xp for every eligible hero.</item>
/// <item>EmptyClanPartiesCampaignBehavior: keeps destroyed player-clan parties to rebuild them later.
/// Keyed on Clan.PlayerClan, so it is not multiplayer-aware yet.</item>
/// <item>PartyConfigurationCampaignBehavior: resets a hero's party commands when they change clan.</item>
/// <item>BattleWreckageCampaignBehavior: naval battle wreckage (War Sails content, unsupported in coop).</item>
/// </list>
/// </summary>
[HarmonyPatch]
internal class DisableV15CampaignBehaviors
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(HeroDailyXpCampaignBehavior), nameof(HeroDailyXpCampaignBehavior.RegisterEvents));
        yield return AccessTools.Method(typeof(EmptyClanPartiesCampaignBehavior), nameof(EmptyClanPartiesCampaignBehavior.RegisterEvents));
        yield return AccessTools.Method(typeof(PartyConfigurationCampaignBehavior), nameof(PartyConfigurationCampaignBehavior.RegisterEvents));
        yield return AccessTools.Method(typeof(BattleWreckageCampaignBehavior), nameof(BattleWreckageCampaignBehavior.RegisterEvents));
    }

    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}

/// <summary>
/// The empty clan parties list belongs to the server's player clan and stops updating on a client
/// once it has loaded the save, so a client would show stale rows and lose party slots for parties
/// that are not its own. Clients report no empty parties.
/// </summary>
[HarmonyPatch(typeof(EmptyClanPartiesCampaignBehavior), nameof(EmptyClanPartiesCampaignBehavior.GetEmptyClanPartyLeaders))]
internal class EmptyClanPartyLeadersClientPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref MBReadOnlyList<Hero> __result)
    {
        if (ModInformation.IsServer) return true;

        __result = new MBList<Hero>();
        return false;
    }
}
