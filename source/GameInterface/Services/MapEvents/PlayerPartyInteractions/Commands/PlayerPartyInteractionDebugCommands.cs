using Common;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions.Commands;

/// <summary>Debug-only commands for driving player-party interactions during live tests.</summary>
internal class PlayerPartyInteractionDebugCommands
{
    [CommandLineArgumentFunction("start", "coop.debug.player_interaction")]
    public static string Start(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run coop.debug.player_interaction.start on the server only";
        if (args.Count != 2)
            return "Usage: coop.debug.player_interaction.start <initiatorControllerId> <responderControllerId>";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return $"Unable to get {nameof(IPlayerManager)}";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return $"Unable to get {nameof(IObjectManager)}";
        if (!ContainerProvider.TryResolve<PlayerPartyInteractionHandler>(out var interactionHandler))
            return $"Unable to get {nameof(PlayerPartyInteractionHandler)}";

        if (!playerManager.TryGetPlayer(args[0], out var initiator))
            return $"Player '{args[0]}' is not registered";
        if (!playerManager.TryGetPlayer(args[1], out var responder))
            return $"Player '{args[1]}' is not registered";
        if (!playerManager.TryGetPeer(initiator.ControllerId, out var initiatorPeer))
            return $"Player '{initiator.ControllerId}' is not connected";
        if (!playerManager.TryGetPeer(responder.ControllerId, out _))
            return $"Player '{responder.ControllerId}' is not connected";
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(initiator.MobilePartyId, out var initiatorParty))
            return $"Player '{initiator.ControllerId}' has no resolved party";
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(responder.MobilePartyId, out var responderParty))
            return $"Player '{responder.ControllerId}' has no resolved party";
        if (!objectManager.TryGetId(initiatorParty.Party, out var initiatorPartyId) ||
            !objectManager.TryGetId(responderParty.Party, out var responderPartyId))
            return "Unable to get player party ids";

        var request = new NetworkRequestConversation(
            responderPartyId,
            initiatorPartyId,
            forcePlayerOutFromSettlement: false,
            ConversationRestartSource.PlayerEncounter,
            armyTalkEncounter: false);

        return interactionHandler.TryStartSession(
            initiatorPeer,
            request,
            initiatorParty.Party,
            responderParty.Party)
            ? $"Started player interaction between {initiator.ControllerId} and {responder.ControllerId}"
            : "Unable to start player interaction";
    }

    [CommandLineArgumentFunction("end", "coop.debug.player_interaction")]
    public static string End(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run coop.debug.player_interaction.end on the server only";
        if (args.Count != 1)
            return "Usage: coop.debug.player_interaction.end <controllerId>";

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return $"Unable to get {nameof(IPlayerManager)}";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return $"Unable to get {nameof(IObjectManager)}";
        if (!ContainerProvider.TryResolve<PlayerPartyInteractionHandler>(out var interactionHandler))
            return $"Unable to get {nameof(PlayerPartyInteractionHandler)}";
        if (!playerManager.TryGetPlayer(args[0], out var player))
            return $"Player '{args[0]}' is not registered";
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party) ||
            !objectManager.TryGetId(party.Party, out var partyId))
            return $"Player '{player.ControllerId}' has no resolved party";

        return interactionHandler.TryEndSessionForDebug(partyId)
            ? $"Ended player interaction for {player.ControllerId}"
            : $"Player '{player.ControllerId}' has no active interaction";
    }
}
