using Common;
using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons.Patches;

/// <summary>
/// The hand-in pays the reward and takes the weapons, so the owner's client hands it to the server. Vanilla only
/// checks the weapons in the hand-in dialog, which is the owner's machine, so the server runs the hand-in as
/// the owning player and refuses it when that player is away from the giver or does not carry them.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.PlayerSuccessfullyDeliveredWeapons))]
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

        if (ModInformation.IsClient)
        {
            return true;
        }

        // The substitution only swaps the party when the owner has one, otherwise the server's own party would be counted and emptied
        if (Hero.MainHero?.PartyBelongedTo == null || Hero.MainHero.PartyBelongedTo != MobileParty.MainParty)
        {
            Logger.Warning("The owner of {quest} has no party to count the weapons in, not completing it", __instance.StringId);
            return false;
        }

        var ownerParty = Hero.MainHero.PartyBelongedTo;
        if (__instance.QuestGiver == null || ownerParty.CurrentSettlement == null || ownerParty.CurrentSettlement != __instance.QuestGiver.CurrentSettlement)
        {
            Logger.Warning("The owner of {quest} is not at the quest giver's settlement, not completing it", __instance.StringId);
            return false;
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
