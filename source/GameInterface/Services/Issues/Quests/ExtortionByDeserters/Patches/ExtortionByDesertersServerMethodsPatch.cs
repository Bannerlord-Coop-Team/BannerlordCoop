using Common;
using GameInterface.AutoSync;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>These world methods must stay server-only even during client replay.</summary>
internal class ExtortionByDesertersServerMethodsPatch
{
    internal static void AddPatches(IAutoSyncPatchCollector collector)
    {
        var prefix = AccessTools.Method(typeof(ExtortionByDesertersServerMethodsPatch), nameof(ServerOnlyPrefix));
        foreach (var method in ServerOnlyMethods())
        {
            collector.AddPrefix(method, prefix);
        }
    }

    internal static IEnumerable<MethodBase> ServerOnlyMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CreateDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CreateDefenderParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.ReleaseDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.ApplyQuestResult));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.DestroyDeserterParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.DestroyDefenderParty));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnMapEventStarted));
    }

    internal static bool ServerOnlyPrefix() => ModInformation.IsServer;
}
