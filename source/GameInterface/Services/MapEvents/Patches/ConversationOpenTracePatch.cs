using Common.Logging;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;

namespace GameInterface.Services.MapEvents.Patches;

// TODO(#3388): temporary diagnostic. Logs EVERY map-conversation open (coop or pure vanilla) with the two
// participant parties and the current army/menu/encounter state, so an in-army member->leader barter that
// bypasses the coop ConversationRequested pipeline still shows up. Remove once the root cause is found.
[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation))]
internal static class ConversationOpenTracePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(ConversationOpenTracePatch));

    [HarmonyPrefix]
    private static void Prefix(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
    {
        try
        {
            var self = playerCharacterData.Party;
            var other = conversationPartnerData.Party;

            Logger.Information(
                "[ConvOpenTrace] OpenMapConversation coopDialogActive={CoopActive} | {Trace}",
                PlayerPartyInteractionDialogState.HasActiveState,
                ConversationRequestHandler.DescribeArmyBarterState(self, other, "conversation-open", armyTalkEncounter: false));
        }
        catch
        {
            // never let the diagnostic break a conversation
        }
    }
}
