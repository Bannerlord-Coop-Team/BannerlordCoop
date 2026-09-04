using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// an interaction with a party while parked in army_wait, calls ConversationManager.OpenMapConversation directly, so 
/// vanilla opens a purely local conversation and the resulting barter is never synced to the other player. This prefix
/// catches the local-player-to-other-player case and goes through the synced player-party interaction pipeline.
/// </summary>
[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation))]
internal static class PlayerToPlayerMapConversationRedirectPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(PlayerToPlayerMapConversationRedirectPatch));

    [HarmonyPrefix]
    internal static bool Prefix(ConversationCharacterData playerCharacterData, ConversationCharacterData conversationPartnerData)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        var self = playerCharacterData.Party;
        var other = conversationPartnerData.Party;
        if (!ShouldRedirect(self?.MobileParty, other?.MobileParty))
        {
            return true;
        }

        Logger.Debug("Redirecting a local player-to-player map conversation into the coop interaction pipeline instead of opening it locally.");

        // armyTalkEncounter: true so the server treats this as a talk/trade and starts the
        // player-party interaction session. Attacker = the local initiator, defender = the other player.
        MessageBroker.Instance.Publish(null, new ConversationRequested(
            other,
            self,
            forcePlayerOutFromSettlement: false,
            ConversationRestartSource.PlayerEncounter,
            armyTalkEncounter: true));

        return false;
    }
    
    internal static bool ShouldRedirect(MobileParty self, MobileParty other)
    {
        if (PlayerPartyInteractionDialogState.HasActiveState)
        {
            return false;
        }

        if (self == null || other == null || ReferenceEquals(self, other))
        {
            return false;
        }

        return self.IsControlledByThisInstance() && other.IsPlayerParty();
    }
}
