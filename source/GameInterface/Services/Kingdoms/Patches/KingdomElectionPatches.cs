using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch]
internal class KingdomElectionPatches
{
    [HarmonyPatch(typeof(KingdomElection), nameof(KingdomElection.OnPlayerSupport))]
    [HarmonyPrefix]
    private static bool Prefix(KingdomElection __instance, DecisionOutcome decisionOutcome, Supporter.SupportWeights supportWeight)
    {
        bool isLocalPlayerChooser = __instance._chooser == Clan.PlayerClan;

        if (!isLocalPlayerChooser)
        {
            foreach (DecisionOutcome outcome in __instance._possibleOutcomes)
            {
                outcome.ResetSupport(__instance.PlayerAsSupporter);
            }
            __instance._hasPlayerVoted = true;
            if (decisionOutcome != null)
            {
                __instance.PlayerAsSupporter.SupportWeight = supportWeight;
                decisionOutcome.AddSupport(__instance.PlayerAsSupporter);
            }
        }
        else
        {
            __instance._chosenOutcome = decisionOutcome;
        }

        return false;
    }
}
