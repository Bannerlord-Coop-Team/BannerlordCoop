#if DEBUG
using Common;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Locations.Conversations.Commands;

internal static class LocationConversationLiveTestProbe
{
    private static int lastAllowedGeneration = -1;
    private static int lastDeniedGeneration = -1;
    private static int lastRequestedGeneration = -1;
    private static int hasApprovedEngagement;
    private static string lastRequestedLocationId;
    private static string lastRequestedCharacterId;

    public static bool Enabled { get; private set; }
    public static int LastAllowedGeneration => Volatile.Read(ref lastAllowedGeneration);
    public static int LastDeniedGeneration => Volatile.Read(ref lastDeniedGeneration);
    public static int LastRequestedGeneration => Volatile.Read(ref lastRequestedGeneration);
    public static bool HasApprovedEngagement => Volatile.Read(ref hasApprovedEngagement) == 1;
    public static string LastRequestedLocationId => Volatile.Read(ref lastRequestedLocationId);
    public static string LastRequestedCharacterId => Volatile.Read(ref lastRequestedCharacterId);

    public static void Enable()
    {
        Enabled = true;
        ResetResponses();
    }

    public static void Disable()
    {
        Enabled = false;
        ResetResponses();
    }

    public static void RecordAllowed(int generation)
    {
        Volatile.Write(ref lastAllowedGeneration, generation);
        Volatile.Write(ref hasApprovedEngagement, 1);
    }

    public static void RecordDenied(int generation) => Volatile.Write(ref lastDeniedGeneration, generation);
    public static void RecordEnded() => Volatile.Write(ref hasApprovedEngagement, 0);

    public static void RecordRequested(int generation, string locationId, string characterId)
    {
        Volatile.Write(ref lastRequestedGeneration, generation);
        Volatile.Write(ref lastRequestedLocationId, locationId);
        Volatile.Write(ref lastRequestedCharacterId, characterId);
    }

    public static void ResetForRealInteraction()
    {
        Enabled = false;
        ResetResponses();
    }

    private static void ResetResponses()
    {
        Volatile.Write(ref lastAllowedGeneration, -1);
        Volatile.Write(ref lastDeniedGeneration, -1);
        Volatile.Write(ref lastRequestedGeneration, -1);
        Volatile.Write(ref hasApprovedEngagement, 0);
        Volatile.Write(ref lastRequestedLocationId, null);
        Volatile.Write(ref lastRequestedCharacterId, null);
    }
}

/// <summary>
/// DEBUG-only commands used by the automated live test for reciprocal location conversations.
/// </summary>
public static class LocationConversationLiveTestCommand
{
    private const string SyntheticLocationId = "Location_live_test";
    private static bool fixtureSnapshotActive;
    private static Settlement fixtureOriginalSettlement;
    private static CampaignVec2 fixtureOriginalPosition;

    [CommandLineArgumentFunction("players", "coop.debug.location_conversation")]
    public static string Players(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.players";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return $"Unable to resolve {nameof(IPlayerManager)}";

        var players = playerManager.Players
            .OrderBy(player => player.ControllerId)
            .Select(player => $"{player.ControllerId},{player.CharacterObjectId},{player.MobilePartyId}");

        return string.Join("|", players);
    }

    [CommandLineArgumentFunction("enable", "coop.debug.location_conversation")]
    public static string Enable(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.enable";
        if (ModInformation.IsServer) return "Command is only available to run on a client";

        LocationConversationLiveTestProbe.Enable();
        return "Location conversation live-test mode enabled";
    }

    [CommandLineArgumentFunction("reset_real", "coop.debug.location_conversation")]
    public static string ResetReal(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.reset_real";
        if (ModInformation.IsServer) return "Command is only available to run on a client";

        LocationConversationLiveTestProbe.ResetForRealInteraction();
        return "Real location-conversation probe reset";
    }

    [CommandLineArgumentFunction("enter_tavern", "coop.debug.location_conversation")]
    public static string EnterTavern(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.location_conversation.enter_tavern <settlementId>";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (Campaign.Current == null || MobileParty.MainParty == null) return "Campaign is not ready";
        if (Mission.Current != null) return $"A mission is already active: {Mission.Current.SceneName}";
        if (PlayerEncounter.Current != null) return "A player encounter is already active";
        if (fixtureSnapshotActive) return "A real tavern fixture snapshot is already active";
        if (!ContainerProvider.TryResolve<ISettlementInterface>(out var settlementInterface))
            return $"Unable to resolve {nameof(ISettlementInterface)}";

        var settlement = Settlement.Find(args[0]);
        if (settlement == null) return $"Unable to find settlement '{args[0]}'";
        if (!settlement.IsTown) return $"Settlement '{args[0]}' is not a town";
        var tavern = settlement.LocationComplex?.GetLocationWithId("tavern");
        if (tavern == null) return $"Settlement '{args[0]}' has no tavern location";

        var mainParty = MobileParty.MainParty;
        fixtureOriginalSettlement = mainParty.CurrentSettlement;
        fixtureOriginalPosition = mainParty.Position;
        fixtureSnapshotActive = true;
        var missionOpened = false;
        try
        {
            using (new AllowedThread())
            {
                if (mainParty.CurrentSettlement != null && mainParty.CurrentSettlement != settlement)
                    settlementInterface.PartyLeaveSettlement(mainParty);

                settlementInterface.StartSettlementEncounter(mainParty, settlement);
                if (PlayerEncounter.Current == null)
                    return $"Unable to start settlement encounter for '{args[0]}'";

                // Vanilla creates the TownEncounter before applying settlement entry. The allowed-thread scope
                // keeps this disposable DEBUG fixture local to the client instead of mutating the server save.
                PlayerEncounter.EnterSettlement();
                var encounter = PlayerEncounter.LocationEncounter;
                if (encounter == null) return $"Unable to create location encounter for '{args[0]}'";

                var center = settlement.LocationComplex.GetLocationWithId("center");
                var controller = encounter.CreateAndOpenMissionController(tavern, center);
                if (controller == null) return $"Unable to open tavern mission for '{args[0]}'";
                missionOpened = true;
            }
        }
        catch (Exception exception)
        {
            return $"Unable to open tavern mission for '{args[0]}': {exception.Message}";
        }
        finally
        {
            if (!missionOpened && fixtureSnapshotActive)
                RestoreFixtureInternal(settlementInterface);
        }

        LocationConversationLiveTestProbe.ResetForRealInteraction();
        return $"Opening real tavern mission in '{args[0]}'";
    }

    [CommandLineArgumentFunction("interact_real", "coop.debug.location_conversation")]
    public static string InteractReal(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.location_conversation.interact_real <targetControllerId>";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (!TryGetRealInteractionTarget(args[0], out var logic, out var targetAgent, out var error))
            return error;

        var priorGeneration = LocationConversationLiveTestProbe.LastRequestedGeneration;
        logic.OnAgentInteraction(Agent.Main, targetAgent, -1);
        var generation = LocationConversationLiveTestProbe.LastRequestedGeneration;
        var gated = generation > priorGeneration;

        return $"Invoked real tavern interaction with '{args[0]}';" +
               $"gated={gated};generation={generation};targetActive={targetAgent.IsActive()}";
    }

    [CommandLineArgumentFunction("position_real", "coop.debug.location_conversation")]
    public static string PositionReal(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.location_conversation.position_real <targetControllerId>";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (!TryGetRealInteractionTarget(args[0], out var logic, out var targetAgent, out var error))
            return error;

        var targetPosition = targetAgent.Position;
        targetPosition.x += 1f;
        Agent.Main.TeleportToPosition(targetPosition);
        var distance = targetAgent.GetDistanceTo(Agent.Main);
        var actionAvailable = logic.IsThereAgentAction(Agent.Main, targetAgent);

        return $"Positioned local player beside real tavern agent '{args[0]}';" +
               $"distance={distance};actionAvailable={actionAvailable}";
    }

    [CommandLineArgumentFunction("can_interact_real", "coop.debug.location_conversation")]
    public static string CanInteractReal(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.location_conversation.can_interact_real <targetControllerId>";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (!TryGetRealInteractionTarget(args[0], out var logic, out var targetAgent, out var error))
            return error;

        return $"Real tavern interaction eligibility for '{args[0]}';" +
               $"distance={targetAgent.GetDistanceTo(Agent.Main)};" +
               $"actionAvailable={logic.IsThereAgentAction(Agent.Main, targetAgent)}";
    }

    [CommandLineArgumentFunction("end_real", "coop.debug.location_conversation")]
    public static string EndReal(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.end_real";
        if (ModInformation.IsServer) return "Command is only available to run on a client";

        var manager = Campaign.Current?.ConversationManager;
        if (manager?.IsConversationInProgress != true) return "No real conversation is in progress";

        manager.EndConversation();
        return "Ended real tavern conversation";
    }

    [CommandLineArgumentFunction("leave_tavern", "coop.debug.location_conversation")]
    public static string LeaveTavern(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.leave_tavern";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (Mission.Current == null) return "No mission is active";

        Mission.Current.EndMission();
        return "Leaving real tavern mission";
    }

    [CommandLineArgumentFunction("finish_settlement", "coop.debug.location_conversation")]
    public static string FinishSettlement(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.finish_settlement";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (Mission.Current != null) return "Wait for the tavern mission to finish first";
        if (!ContainerProvider.TryResolve<ISettlementInterface>(out var settlementInterface))
            return $"Unable to resolve {nameof(ISettlementInterface)}";

        using (new AllowedThread())
            settlementInterface.EndSettlementEncounter();

        return "Finished local settlement encounter";
    }

    [CommandLineArgumentFunction("restore_fixture", "coop.debug.location_conversation")]
    public static string RestoreFixture(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.restore_fixture";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (Mission.Current != null) return "Wait for the tavern mission to finish first";
        if (!fixtureSnapshotActive) return "No real tavern fixture snapshot is active";
        if (!ContainerProvider.TryResolve<ISettlementInterface>(out var settlementInterface))
            return $"Unable to resolve {nameof(ISettlementInterface)}";

        var originalSettlementId = fixtureOriginalSettlement?.StringId ?? "none";
        RestoreFixtureInternal(settlementInterface);
        return $"Restored original local fixture;settlement={originalSettlementId};" +
               $"position={MobileParty.MainParty.Position.X:R},{MobileParty.MainParty.Position.Y:R}";
    }

    [CommandLineArgumentFunction("disable", "coop.debug.location_conversation")]
    public static string Disable(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.disable";
        if (ModInformation.IsServer) return "Command is only available to run on a client";

        if (LocationConversationLiveTestProbe.HasApprovedEngagement ||
            LocationPlayerInteractionWaitingOverlay.Instance.IsShownForLiveTest)
        {
            return "End the active synthetic interaction before disabling live-test mode";
        }

        LocationConversationLiveTestProbe.Disable();
        return "Location conversation live-test mode disabled";
    }

    [CommandLineArgumentFunction("request", "coop.debug.location_conversation")]
    public static string Request(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.location_conversation.request <targetControllerId> <generation>";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (!LocationConversationLiveTestProbe.Enabled) return "Enable location conversation live-test mode first";
        if (!int.TryParse(args[1], out var generation)) return $"Invalid generation '{args[1]}'";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return $"Unable to resolve {nameof(IPlayerManager)}";
        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return $"Unable to resolve {nameof(INetwork)}";

        var target = playerManager.Players.SingleOrDefault(player => player.ControllerId == args[0]);
        if (target == null) return $"Unable to find player '{args[0]}'";
        if (string.IsNullOrEmpty(target.CharacterObjectId))
            return $"Player '{args[0]}' has no character object id";

        network.SendAll(new NetworkRequestLocationConversation(
            SyntheticLocationId,
            target.CharacterObjectId,
            generation));

        return $"Requested location conversation with '{args[0]}' using generation {generation}";
    }

    [CommandLineArgumentFunction("end", "coop.debug.location_conversation")]
    public static string End(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.end";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return $"Unable to resolve {nameof(INetwork)}";

        network.SendAll(new NetworkLocationConversationEnded());
        LocationConversationLiveTestProbe.RecordEnded();
        return "Ended location conversation";
    }

    [CommandLineArgumentFunction("status", "coop.debug.location_conversation")]
    public static string Status(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.status";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return $"Unable to resolve {nameof(IPlayerManager)}";
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider))
            return $"Unable to resolve {nameof(IControllerIdProvider)}";

        var trackerState = ModInformation.IsServer
            ? (LocationConversationTracker.Instance?.IsEmpty.ToString() ?? "<unavailable>")
            : "<n/a>";
        var overlay = LocationPlayerInteractionWaitingOverlay.Instance;
        var mission = Mission.Current;
        var conversationManager = Campaign.Current?.ConversationManager;
        var location = CampaignMission.Current?.Location;
        var mainAgent = Agent.Main;
        var mainParty = MobileParty.MainParty;
        var remoteAgents = GetRemotePlayerAgents(playerManager).ToArray();

        return $"enabled={LocationConversationLiveTestProbe.Enabled};" +
               $"controller={controllerIdProvider.ControllerId};" +
               $"players={playerManager.Players.Count};" +
               $"lastAllowed={LocationConversationLiveTestProbe.LastAllowedGeneration};" +
               $"lastDenied={LocationConversationLiveTestProbe.LastDeniedGeneration};" +
               $"lastRequested={LocationConversationLiveTestProbe.LastRequestedGeneration};" +
               $"requestedLocation={LocationConversationLiveTestProbe.LastRequestedLocationId ?? "<none>"};" +
               $"requestedCharacter={LocationConversationLiveTestProbe.LastRequestedCharacterId ?? "<none>"};" +
               $"hasApproved={LocationConversationLiveTestProbe.HasApprovedEngagement};" +
               $"overlayShown={overlay.IsShownForLiveTest};" +
               $"overlayText={overlay.WaitingTextForLiveTest ?? "<none>"};" +
               $"missionActive={mission != null};" +
               $"missionScene={mission?.SceneName ?? "<none>"};" +
               $"missionMode={mission?.Mode.ToString() ?? "<none>"};" +
               $"settlement={mainParty?.CurrentSettlement?.StringId ?? "<none>"};" +
               $"location={location?.StringId ?? "<none>"};" +
               $"conversationInProgress={conversationManager?.IsConversationInProgress ?? false};" +
               $"mainAgentActive={mainAgent?.IsActive() ?? false};" +
               $"mainAgentController={mainAgent?.Controller.ToString() ?? "<none>"};" +
               $"mainAgentUsingObject={mainAgent?.IsUsingGameObject ?? false};" +
               $"remotePlayerAgents={string.Join(",", remoteAgents.Select(agent => $"{((CharacterObject)agent.Character).StringId}@{agent.Position}:distance={(mainAgent == null ? -1f : agent.GetDistanceTo(mainAgent))}"))};" +
               $"playerEncounterActive={PlayerEncounter.Current != null};" +
               $"fixtureSnapshotActive={fixtureSnapshotActive};" +
               $"fixtureOriginalSettlement={fixtureOriginalSettlement?.StringId ?? "none"};" +
               $"partyPosition={(mainParty == null ? "none" : $"{mainParty.Position.X:R},{mainParty.Position.Y:R}")};" +
               $"trackerEmpty={trackerState}";
    }

    [CommandLineArgumentFunction("mark", "coop.debug.location_conversation")]
    public static string Mark(List<string> args)
    {
        if (args.Count != 1) return "Usage: coop.debug.location_conversation.mark <label>";
        if (ModInformation.IsServer) return "Command is only available to run on a client";

        var label = args[0];
        GameThread.Run(
            () => InformationManager.DisplayMessage(new InformationMessage($"LIVE TEST: {label}")),
            blocking: true);
        return $"Displayed live-test label '{label}'";
    }

    private static bool TryGetRealInteractionTarget(
        string controllerId,
        out MissionConversationLogic logic,
        out Agent targetAgent,
        out string error)
    {
        logic = null;
        targetAgent = null;
        error = null;

        if (Mission.Current == null || CampaignMission.Current?.Location == null)
        {
            error = "A real location mission is not active";
            return false;
        }
        if (Agent.Main?.IsActive() != true)
        {
            error = "The local player agent is not active";
            return false;
        }
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = $"Unable to resolve {nameof(IPlayerManager)}";
            return false;
        }

        var player = playerManager.Players.SingleOrDefault(candidate => candidate.ControllerId == controllerId);
        if (player == null)
        {
            error = $"Unable to find player '{controllerId}'";
            return false;
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetObject<CharacterObject>(player.CharacterObjectId, out var playerCharacter))
        {
            error = $"Unable to resolve character '{player.CharacterObjectId}' for player '{controllerId}'";
            return false;
        }

        var matches = GetRemotePlayerAgents(playerManager)
            .Where(agent => ReferenceEquals(agent.Character, playerCharacter))
            .ToArray();
        if (matches.Length != 1)
        {
            error = $"Expected one active tavern agent for '{controllerId}', found {matches.Length}";
            return false;
        }

        logic = Mission.Current.GetMissionBehavior<MissionConversationLogic>();
        if (logic == null)
        {
            error = "The real MissionConversationLogic is not active";
            return false;
        }

        targetAgent = matches[0];
        return true;
    }

    private static void RestoreFixtureInternal(ISettlementInterface settlementInterface)
    {
        using (new AllowedThread())
        {
            if (PlayerEncounter.Current != null)
                PlayerEncounter.Finish(forcePlayerOutFromSettlement: false);

            var mainParty = MobileParty.MainParty;
            if (mainParty.CurrentSettlement != null && mainParty.CurrentSettlement != fixtureOriginalSettlement)
                settlementInterface.PartyLeaveSettlement(mainParty);
            if (fixtureOriginalSettlement != null && mainParty.CurrentSettlement != fixtureOriginalSettlement)
                settlementInterface.PartyEnterSettlement(mainParty, fixtureOriginalSettlement);

            mainParty.Position = fixtureOriginalPosition;
        }

        fixtureSnapshotActive = false;
        fixtureOriginalSettlement = null;
        fixtureOriginalPosition = default;
    }

    private static IEnumerable<Agent> GetRemotePlayerAgents(IPlayerManager playerManager)
    {
        if (Mission.Current == null) return Enumerable.Empty<Agent>();

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return Enumerable.Empty<Agent>();

        var remoteCharacters = new HashSet<CharacterObject>();
        foreach (var player in playerManager.Players)
        {
            if (!string.IsNullOrEmpty(player.CharacterObjectId) &&
                objectManager.TryGetObject<CharacterObject>(player.CharacterObjectId, out var character))
            {
                remoteCharacters.Add(character);
            }
        }

        return Mission.Current.Agents.Where(agent =>
            agent != Agent.Main &&
            agent?.IsActive() == true &&
            agent.Character is CharacterObject character &&
            character.IsHero &&
            remoteCharacters.Contains(character));
    }
}
#endif
