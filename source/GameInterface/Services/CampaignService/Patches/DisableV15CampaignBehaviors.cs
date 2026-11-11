using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace GameInterface.Services.CampaignService.Patches;

/// <summary>
/// Campaign behaviors added in v1.5 that change synchronized campaign state. Like the other disabled
/// behaviors, they run on the server only; their results reach clients through the usual sync.
/// <list type="bullet">
/// <item>HeroDailyXpCampaignBehavior: daily skill xp for every eligible hero, players excluded
/// (<see cref="HeroDailyXpPlayerHeroPatch"/>).</item>
/// <item>EmptyClanPartiesCampaignBehavior: keeps destroyed player-clan parties to rebuild them later.
/// Keyed on Clan.PlayerClan, so it is not multiplayer-aware yet.</item>
/// <item>PartyConfigurationCampaignBehavior: resets a hero's party commands. Its reset is not synced
/// by itself; PartyConfigurationCampaignBehaviorPatches makes it reach clients.</item>
/// </list>
/// </summary>
[HarmonyPatch]
internal class DisableV15CampaignBehaviors
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(HeroDailyXpCampaignBehavior), nameof(HeroDailyXpCampaignBehavior.RegisterEvents));
        yield return AccessTools.Method(typeof(EmptyClanPartiesCampaignBehavior), nameof(EmptyClanPartiesCampaignBehavior.RegisterEvents));
        yield return AccessTools.Method(typeof(PartyConfigurationCampaignBehavior), nameof(PartyConfigurationCampaignBehavior.RegisterEvents));
    }

    [HarmonyPrefix]
    internal static bool Prefix() => ModInformation.IsServer;
}

/// <summary>
/// Vanilla gives no daily skill xp to the player's own hero. Co-op player heroes are not the server's
/// Hero.MainHero, so the behavior would give them xp; skip them the same way.
/// </summary>
[HarmonyPatch(typeof(HeroDailyXpCampaignBehavior), nameof(HeroDailyXpCampaignBehavior.DailyTickHero))]
internal class HeroDailyXpPlayerHeroPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Hero hero) => !hero.IsPlayerHero();
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

/// <summary>
/// Battle wreckages, the land battle sites and sea wreckages left after a battle, are off on every role:
/// they have no registry or sync yet, and co-op drops the StringId of a server-made one.
/// </summary>
[HarmonyPatch(typeof(BattleWreckageCampaignBehavior), nameof(BattleWreckageCampaignBehavior.RegisterEvents))]
internal class DisableBattleWreckageCampaignBehavior
{
    [HarmonyPrefix]
    internal static bool Prefix() => false;
}

/// <summary>
/// Drops every saved wreckage before the object lists are built, since with the behavior off they never
/// expire and clicking one opens a menu that was never registered. Id-less ones also throw while loading.
/// </summary>
[HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.InitializeOnLoad))]
internal class SavedWreckageRemovalPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SavedWreckageRemovalPatch>();

    // Removes the saved wreckages and logs how many were dropped.
    [HarmonyPrefix]
    private static void Prefix(CampaignObjectManager __instance)
    {
        var removed = RemoveSavedWreckages(__instance);
        if (removed > 0) Logger.Information("Dropped {Count} saved battle wreckages", removed);
    }

    // Clears the saved wreckage list and returns how many entries it held.
    internal static int RemoveSavedWreckages(CampaignObjectManager manager)
    {
        var wreckages = manager._wreckages;
        if (wreckages == null) return 0;

        var count = wreckages.Count;
        wreckages.Clear();
        return count;
    }
}
