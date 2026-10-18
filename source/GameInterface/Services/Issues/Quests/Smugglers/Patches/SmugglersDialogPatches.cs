using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace GameInterface.Services.Issues.Quests.Smugglers.Patches;

/// <summary>
/// Vanilla opens the smugglers' dialog for any party led by the same caravan master troop, which every smugglers'
/// party shares. In co-op each player can run their own Smugglers quest, so a quest only talks to its own party.
/// </summary>
[HarmonyPatch]
internal class SmugglersDialogPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<SmugglersDialogPatches>();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        if (FindLeaderCondition() != null)
        {
            return true;
        }

        Logger.Warning("The smugglers' leader dialog condition was not found, their dialog opens for every smugglers' party");
        return false;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod() => FindLeaderCondition();

    [HarmonyPostfix]
    private static void Postfix(Quest __instance, ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        var party = MobileParty.ConversationParty ?? PlayerEncounter.EncounteredMobileParty;
        if (party != null && party != __instance._smugglerParty)
        {
            __result = false;
        }
    }

    // The condition is a compiler generated lambda inside GetSmugglerPartyDialog, so it has no name to bind to
    internal static MethodInfo FindLeaderCondition()
    {
        var conditions = AccessTools.GetDeclaredMethods(typeof(Quest))
            .Where(method =>
                method.Name.StartsWith("<" + nameof(Quest.GetSmugglerPartyDialog) + ">") &&
                method.ReturnType == typeof(bool) &&
                method.GetParameters().Length == 0)
            .ToList();

        // Another bool lambda there would make the match ambiguous
        return conditions.Count == 1 ? conditions[0] : null;
    }
}
