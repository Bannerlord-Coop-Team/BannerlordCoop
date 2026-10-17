using Common;
using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons.Patches;

/// <summary>
/// The hand-in pays the reward and takes the weapons, so the owner's client hands it to the server. Vanilla only
/// checks the weapons in the hand-in dialog, which is the owner's machine, so the server runs the hand-in as
/// the owning player and refuses it when that player does not carry them.
/// </summary>
[HarmonyPatch(typeof(Quest), "PlayerSuccessfullyDeliveredWeapons")]
internal class GangLeaderNeedsWeaponsDeliveryPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderNeedsWeaponsDeliveryPatch>();

    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        if (!dispatcher.BeforeQuestBranch(__instance, (byte)GangLeaderNeedsWeaponsBranch.DeliverWeapons))
        {
            return false;
        }

        if (!ModInformation.IsServer)
        {
            return true;
        }

        __instance.CalculateAndSetRequestedItemCountOnPlayer();

        if (__instance._collectedItemAmount >= __instance._requestedWeaponAmount)
        {
            return true;
        }

        Logger.Warning(
            "The owner of {quest} carries {carried} of the {needed} weapons, not completing it",
            __instance.StringId,
            __instance._collectedItemAmount,
            __instance._requestedWeaponAmount);

        return false;
    }
}
