#if DEBUG
using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

public class RejectedEncounterFixtureCommands
{
    private sealed class Fixture
    {
        public string ControllerId;
        public string Kind;
        public Settlement Settlement;
        public MobileParty PlayerParty;
        public MobileParty HostileParty;
        public PartyBehaviorUpdateData PlayerBehavior;
    }

    private static Fixture fixture;

    [CommandLineArgumentFunction("rejected_encounter_fixture_start", "coop.debug.mapevent")]
    public static string Start(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2 || (args[1] != "deserter" && args[1] != "looter"))
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_start <controllerId> <deserter|looter>";

        if (fixture != null)
            return "A rejected encounter fixture is already active.";

        if (!TryGetPlayerParty(args[0], out var playerParty, out var error))
            return error;

        if (playerParty.CurrentSettlement != null)
            return "The player party must be outside a settlement before fixture setup.";

        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !behaviorSnapshot.TryCreate(playerParty, out var playerBehavior))
            return "Unable to capture the player party behavior.";

        var settlementId = args[1] == "deserter" ? "town_B2" : "town_S3";
        var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == settlementId);
        if (settlement == null)
            return $"Fixture settlement {settlementId} was not found.";

        var kindClanId = args[1] == "deserter" ? "deserters" : "looters";
        var kindClan = Clan.BanditFactions.FirstOrDefault(clan => clan.StringId == kindClanId);
        if (kindClan == null)
            return $"Bandit faction {kindClanId} is unavailable.";

        var partyTemplate = kindClan.DefaultPartyTemplate;
        if (partyTemplate == null)
            return $"Bandit faction {kindClanId} has no default party template.";

        var playerPosition = new CampaignVec2(
            new Vec2(settlement.GatePosition.X + 3f, settlement.GatePosition.Y + 1f),
            isOnLand: true);
        var hostilePosition = new CampaignVec2(
            new Vec2(settlement.GatePosition.X + 3.6f, settlement.GatePosition.Y + 1f),
            isOnLand: true);
        var activeFixture = new Fixture
        {
            ControllerId = args[0],
            Kind = args[1],
            Settlement = settlement,
            PlayerParty = playerParty,
            PlayerBehavior = playerBehavior,
        };
        fixture = activeFixture;
        if (ContainerProvider.TryResolve<MapEventCreationCoordinator>(out var coordinator))
            coordinator.ClearDebugRejection();

        try
        {
            MoveAndHold(playerParty, playerPosition);
            activeFixture.HostileParty = args[1] == "deserter"
                ? CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                    hostilePosition,
                    spawnRadius: 0f,
                    homeSettlement: null,
                    new TextObject("Deserters"),
                    kindClan,
                    partyTemplate,
                    owner: null)
                : BanditPartyComponent.CreateLooterParty(
                    "debug_2399_looters_" + Guid.NewGuid().ToString("N"),
                    kindClan,
                    settlement,
                    isBossParty: false,
                    partyTemplate,
                    hostilePosition);
            if (activeFixture.HostileParty == null || !activeFixture.HostileParty.IsActive)
                throw new InvalidOperationException("The hostile fixture party did not become active.");

            MoveAndHold(activeFixture.HostileParty, hostilePosition);
            if (!TryGetObjectManager(out var objectManager) ||
                !objectManager.TryGetId(playerParty.Party, out var playerPartyBaseId) ||
                !objectManager.TryGetId(activeFixture.HostileParty.Party, out var hostilePartyBaseId))
                throw new InvalidOperationException("Unable to resolve the fixture PartyBase ids.");

            return $"Rejected encounter fixture staged: kind={args[1]}, settlement={settlement.Name}, " +
                   $"playerPartyBaseId={playerPartyBaseId}, hostilePartyBaseId={hostilePartyBaseId}, " +
                   $"original={playerBehavior.PartyPosition.X:R}|{playerBehavior.PartyPosition.Y:R}, " +
                   $"originalSea={playerBehavior.IsCurrentlyAtSea}, " +
                   $"player={playerPosition.X:R}|{playerPosition.Y:R}, hostile={hostilePosition.X:R}|{hostilePosition.Y:R}.";
        }
        catch (Exception exception)
        {
            var cleanup = RestoreActiveFixture();
            return $"Rejected encounter fixture setup failed: {exception.Message}. {cleanup}";
        }
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_reject_next", "coop.debug.mapevent")]
    public static string RejectNext(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_reject_next";

        if (!TryGetFixturePartyIds(out var playerPartyBaseId, out var hostilePartyBaseId, out var error))
            return error;

        if (!ContainerProvider.TryResolve<MapEventCreationCoordinator>(out var coordinator))
            return "Unable to resolve the map event creation coordinator.";

        return "Forced rejection " + coordinator.ArmDebugRejection(playerPartyBaseId, hostilePartyBaseId) + ".";
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_trigger", "coop.debug.mapevent")]
    public static string Trigger(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_trigger";

        var activeFixture = fixture;
        if (activeFixture == null)
            return "No rejected encounter fixture is active.";

        if (activeFixture.PlayerParty.MapEvent != null || activeFixture.HostileParty.MapEvent != null)
            return "The fixture parties must be outside a map event before triggering.";

        EncounterManager.StartPartyEncounter(activeFixture.HostileParty.Party, activeFixture.PlayerParty.Party);
        return $"Triggered the rendered {activeFixture.Kind} encounter beside {activeFixture.Settlement.Name}.";
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_start_battle_only", "coop.debug.mapevent")]
    public static string StartBattleOnly(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_start_battle_only";

        if (PlayerEncounter.Current == null)
            return "No active encounter.";

        var conversationManager = Campaign.Current?.ConversationManager;
        if (conversationManager?.IsConversationInProgress == true)
            conversationManager.EndConversation();

        var mapEvent = PlayerEncounter.Battle ?? PlayerEncounter.StartBattle();
        if (mapEvent == null)
            return "Unable to create the authoritative map event.";

        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetId(mapEvent, out var mapEventId))
            return "The authoritative map event has no registered id.";

        return $"Created authoritative map event {mapEventId} without requesting a mission.";
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_focus_settlement", "coop.debug.mapevent")]
    public static string FocusSettlement(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_focus_settlement <settlementId>";

        var settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == args[0]);
        if (settlement == null)
            return $"Settlement {args[0]} was not found.";

        settlement.Party.SetAsCameraFollowParty();
        return $"Focused the campaign camera on {settlement.Name} ({settlement.StringId}).";
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_focus_player", "coop.debug.mapevent")]
    public static string FocusPlayer(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_focus_player";

        if (MobileParty.MainParty == null)
            return "Player party is unavailable.";

        MobileParty.MainParty.Party.SetAsCameraFollowParty();
        return "Focused the campaign camera on the player party.";
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_player_state", "coop.debug.mapevent")]
    public static string PlayerState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_player_state";

        var playerParty = MobileParty.MainParty;
        return playerParty == null
            ? "Player party: <null>."
            : $"Player party: position={playerParty.Position.X:R}|{playerParty.Position.Y:R}, " +
              $"isCurrentlyAtSea={playerParty.IsCurrentlyAtSea}.";
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_party_state", "coop.debug.mapevent")]
    public static string PartyState(List<string> args)
    {
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_party_state <partyBaseId>";

        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetObject<PartyBase>(args[0], out var party))
            return $"Fixture party {args[0]}: ready=False, resolved=False.";

        var memberRoster = party.MemberRoster;
        var memberCount = memberRoster?.TotalManCount ?? 0;
        var conversationCharacter = memberCount > 0
            ? ConversationHelper.GetConversationCharacterPartyLeader(party)
            : null;
        var mobileParty = party.MobileParty;
        var mapFaction = mobileParty?.MapFaction;
        var ready = mobileParty != null &&
                    mapFaction != null &&
                    conversationCharacter != null;
        return $"Fixture party {args[0]}: ready={ready}, resolved=True, memberCount={memberCount}, " +
               $"mapFaction={mapFaction?.StringId ?? "<null>"}, " +
               $"conversationCharacter={conversationCharacter?.StringId ?? "<null>"}.";
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_state", "coop.debug.mapevent")]
    public static string State(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_state";

        var activeFixture = fixture;
        if (activeFixture == null)
            return "Fixture: active=False.";

        var hookState = ContainerProvider.TryResolve<MapEventCreationCoordinator>(out var coordinator)
            ? coordinator.GetDebugRejectionState()
            : "unavailable";
        return $"Fixture: kind={activeFixture.Kind}, settlement={activeFixture.Settlement.Name}, " +
               $"player={FormatPartyState(activeFixture.PlayerParty)}, hostile={FormatPartyState(activeFixture.HostileParty)}, " +
               $"rejection={hookState}.";
    }

    [CommandLineArgumentFunction("rejected_encounter_fixture_restore", "coop.debug.mapevent")]
    public static string Restore(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.rejected_encounter_fixture_restore";

        return RestoreActiveFixture();
    }

    private static string RestoreActiveFixture()
    {
        var activeFixture = fixture;
        if (activeFixture == null)
            return "Rejected encounter fixture already restored.";

        try
        {
            if (ContainerProvider.TryResolve<MapEventCreationCoordinator>(out var coordinator))
                coordinator.ClearDebugRejection();

            var mapEvent = activeFixture.PlayerParty.MapEvent;
            if (mapEvent != null &&
                mapEvent == activeFixture.HostileParty?.MapEvent &&
                !mapEvent.IsFinalized)
                mapEvent.FinalizeEvent();

            if (activeFixture.HostileParty?.IsActive == true && activeFixture.HostileParty.MapEvent == null)
                DestroyPartyAction.Apply(null, activeFixture.HostileParty);

            ApplyPositionAndSeaState(
                activeFixture.PlayerParty,
                activeFixture.PlayerBehavior.PartyPosition,
                activeFixture.PlayerBehavior.IsCurrentlyAtSea);
            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
                !behaviorSnapshot.TryApply(activeFixture.PlayerParty, activeFixture.PlayerBehavior, out _))
                throw new InvalidOperationException("Unable to restore the player party behavior.");

            if (activeFixture.PlayerParty.Position != activeFixture.PlayerBehavior.PartyPosition ||
                activeFixture.PlayerParty.IsCurrentlyAtSea != activeFixture.PlayerBehavior.IsCurrentlyAtSea)
                throw new InvalidOperationException("The player party position or sea state was not restored.");

            MessageBroker.Instance.Publish(
                typeof(RejectedEncounterFixtureCommands),
                new PartyBehaviorChangeAttempted(
                    activeFixture.PlayerParty,
                    forcePosition: true,
                    isCurrentlyAtSea: activeFixture.PlayerBehavior.IsCurrentlyAtSea,
                    resetMovementToHold: false));
            fixture = null;
            return $"Rejected encounter fixture restored: " +
                   $"position={activeFixture.PlayerBehavior.PartyPosition.X:R}|{activeFixture.PlayerBehavior.PartyPosition.Y:R}, " +
                   $"isCurrentlyAtSea={activeFixture.PlayerBehavior.IsCurrentlyAtSea}.";
        }
        catch (Exception exception)
        {
            return $"Rejected encounter fixture restore failed: {exception.Message}";
        }
    }

    private static bool TryGetFixturePartyIds(out string playerPartyBaseId, out string hostilePartyBaseId, out string error)
    {
        playerPartyBaseId = null;
        hostilePartyBaseId = null;
        error = null;
        if (fixture == null)
        {
            error = "No rejected encounter fixture is active.";
            return false;
        }

        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetId(fixture.PlayerParty.Party, out playerPartyBaseId) ||
            !objectManager.TryGetId(fixture.HostileParty.Party, out hostilePartyBaseId))
        {
            error = "Unable to resolve the fixture PartyBase ids.";
            return false;
        }

        return true;
    }

    private static bool TryGetPlayerParty(string controllerId, out MobileParty playerParty, out string error)
    {
        playerParty = null;
        error = null;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(controllerId, out var player) ||
            !playerManager.IsConnected(player))
        {
            error = $"Player {controllerId} is not connected.";
            return false;
        }

        if (!TryGetObjectManager(out var objectManager) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out playerParty))
        {
            error = $"Unable to resolve player party {player.MobilePartyId}.";
            return false;
        }

        if (!playerParty.IsActive || playerParty.MapEvent != null)
        {
            error = "The player party must be active and outside a map event.";
            return false;
        }

        return true;
    }

    private static bool TryGetObjectManager(out IObjectManager objectManager) =>
        ContainerProvider.TryResolve(out objectManager);

    private static void MoveAndHold(MobileParty party, CampaignVec2 position)
    {
        ApplyPositionAndSeaState(party, position, isCurrentlyAtSea: false);
        party.SetMoveModeHold();
        party.ResetNavigationToHold();
        MessageBroker.Instance.Publish(
            typeof(RejectedEncounterFixtureCommands),
            new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: false, resetMovementToHold: true));
    }

    private static void ApplyPositionAndSeaState(
        MobileParty party,
        CampaignVec2 position,
        bool isCurrentlyAtSea)
    {
        party.Position = position;
        if (party.IsCurrentlyAtSea != isCurrentlyAtSea)
            party.ChangeIsCurrentlyAtSeaCheat();
    }

    private static string FormatPartyState(MobileParty party)
    {
        if (party == null)
            return "<null>";

        return $"{party.StringId}@{party.Position.X:R}|{party.Position.Y:R}|active={party.IsActive}|mapEvent={party.MapEvent != null}";
    }
}
#endif
