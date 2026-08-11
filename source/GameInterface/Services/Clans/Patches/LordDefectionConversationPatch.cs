using Common;
using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Blocks the barter-less lord defection branch on clients.
/// </summary>
/// <remarks>
/// The supported recruitment path goes through <see cref="TaleWorlds.CampaignSystem.BarterSystem.BarterManager"/>,
/// which LordBarterPatch intercepts and routes to the server. This sibling branch would instead call
/// JoinKingdomAsClanBarterable.Apply() directly in the conversation consequence, moving the clan on the
/// client with nothing replicated.
///
/// It is unreachable on the shipped v1.4.7 assembly - its gate,
/// conversation_lord_check_if_ready_to_join_faction_without_barter_on_condition, is compiled to a
/// hardcoded `return false` (IL: ldc.i4.0; ret). This guard exists so a future game patch that
/// enables the branch cannot silently desync kingdom membership.
/// </remarks>
[HarmonyPatch(typeof(LordDefectionCampaignBehavior))]
internal class LordDefectionConversationPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordDefectionConversationPatch>();

    [HarmonyPatch(nameof(LordDefectionCampaignBehavior.conversation_lord_defect_to_clan_without_barter_on_consequence))]
    [HarmonyPrefix]
    private static bool ConversationLordDefectToClanWithoutBarterOnConsequencePrefix()
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;

        Logger.Warning("Suppressed client-local lord defection taken through the barter-less branch");
        return false;
    }
}
