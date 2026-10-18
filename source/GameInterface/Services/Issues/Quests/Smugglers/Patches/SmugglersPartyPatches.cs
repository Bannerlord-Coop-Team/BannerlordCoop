using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace GameInterface.Services.Issues.Quests.Smugglers.Patches;

/// <summary>
/// The smugglers' party is spawned and removed by the server, the owner's quest only points at it.
/// </summary>
[HarmonyPatch]
internal class SmugglersPartyPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.CreateSmugglerParty))]
    [HarmonyPrefix]
    private static bool CreateSmugglerPartyPrefix(Quest __instance, ref MobileParty __result)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        // Keeps the server's party when it arrived before vanilla's accept consequence ran
        __result = __instance._smugglerParty;
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool OnFinalizePrefix() => ModInformation.IsServer;

    // Dismissing an issue drops its running quest without finishing it, which would leave the smugglers on the map
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalizedPrefix(IssueBase __instance)
    {
        if (ModInformation.IsClient || !(__instance.IssueQuest is Quest quest) || !quest.IsOngoing)
        {
            return;
        }

        var party = quest._smugglerParty;
        if (party != null && party.IsActive)
        {
            DestroyPartyAction.Apply(null, party);
        }
    }
}
