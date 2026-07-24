#if DEBUG
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

/// <summary>Restorable open-field player-versus-player fixture for automated battle tests.</summary>
internal static class PvpBattleLiveTestCommands
{
    private sealed class Fixture
    {
        public string SessionId;
        public PartyBase InitiatorParty;
        public PartyBase ResponderParty;
        public MapEvent MapEvent;
        public PartyBase[] InvolvedParties;
        public bool InitiatorWasActive;
        public bool ResponderWasActive;
        public Settlement InitiatorSettlement;
        public Settlement ResponderSettlement;
        public CampaignVec2 InitiatorPosition;
        public CampaignVec2 ResponderPosition;
    }

    private static Fixture activeFixture;

    [CommandLineArgumentFunction("pvp_fixture_start", "coop.debug.mapevent")]
    public static string Start(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 2)
            return "Usage: coop.debug.mapevent.pvp_fixture_start <initiatorControllerId> <responderControllerId>";

        if (activeFixture != null)
            return $"PvP fixture already active: {activeFixture.SessionId}.";

        if (!TryResolveParties(args[0], args[1], out var objectManager, out var initiator, out var responder, out var error))
            return error;

        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return "Unable to resolve the network service.";

        var fixture = new Fixture
        {
            SessionId = $"debug-2311-{Guid.NewGuid():N}",
            InitiatorParty = initiator,
            ResponderParty = responder,
            InitiatorWasActive = initiator.MobileParty.IsActive,
            ResponderWasActive = responder.MobileParty.IsActive,
            InitiatorSettlement = initiator.MobileParty.CurrentSettlement,
            ResponderSettlement = responder.MobileParty.CurrentSettlement,
            InitiatorPosition = initiator.MobileParty.Position,
            ResponderPosition = responder.MobileParty.Position,
        };

        try
        {
            PreparePartyForFieldBattle(initiator.MobileParty);
            PreparePartyForFieldBattle(responder.MobileParty);

            fixture.MapEvent = MapEventBattleFactory.CreateMapEvent(initiator, responder, default);
            if (fixture.MapEvent == null)
            {
                throw new InvalidOperationException(
                    "The player-versus-player field encounter was not created after both connected " +
                    "player parties were activated and detached from settlements.");
            }

            fixture.InvolvedParties = fixture.MapEvent.InvolvedParties.ToArray();
            if (initiator.MapEvent != fixture.MapEvent || responder.MapEvent != fixture.MapEvent)
                throw new InvalidOperationException("The player-versus-player field encounter did not attach both parties.");

            if (!objectManager.TryGetId(initiator, out var initiatorPartyId) ||
                !objectManager.TryGetId(responder, out var responderPartyId) ||
                !objectManager.TryGetId(fixture.MapEvent, out var mapEventId))
            {
                throw new InvalidOperationException("Unable to resolve the fixture's network ids.");
            }

            network.SendAll(new NetworkPlayerPartyHostileEncounterStarted(
                fixture.SessionId,
                initiatorPartyId,
                responderPartyId,
                mapEventId));
            activeFixture = fixture;
        }
        catch (Exception e)
        {
            activeFixture = fixture;
            if (TryRestoreFixture(fixture, out var restoreError))
                activeFixture = null;
            else
                return $"Fixture setup failed: {e.Message}. Cleanup failed: {restoreError}. Run the restore command.";

            return $"Fixture setup failed: {e.Message}";
        }

        objectManager.TryGetId(fixture.MapEvent, out string fixtureMapEventId);
        return $"PvP fixture started: session={fixture.SessionId}, mapEvent={fixtureMapEventId}, " +
               $"initiator={args[0]}, responder={args[1]}.";
    }

    [CommandLineArgumentFunction("pvp_fixture_state", "coop.debug.mapevent")]
    public static string State(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.pvp_fixture_state";

        if (activeFixture == null)
            return "PvP fixture inactive.";

        var mapEvent = activeFixture.MapEvent;
        return $"PvP fixture active: session={activeFixture.SessionId}, " +
               $"mapEventFinalized={mapEvent?.IsFinalized}, " +
               $"initiatorAttached={activeFixture.InitiatorParty.MapEvent == mapEvent}, " +
               $"responderAttached={activeFixture.ResponderParty.MapEvent == mapEvent}.";
    }

    [CommandLineArgumentFunction("pvp_fixture_restore", "coop.debug.mapevent")]
    public static string Restore(List<string> args)
    {
        if (ModInformation.IsClient)
            return "Run this command on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.pvp_fixture_restore";

        if (activeFixture == null)
            return "PvP fixture inactive.";

        var fixture = activeFixture;
        if (!TryRestoreFixture(fixture, out var error))
            return $"PvP fixture restore incomplete: {error}.";

        activeFixture = null;
        return $"PvP fixture restored: session={fixture.SessionId}, partiesAttached=false.";
    }

    private static bool TryResolveParties(
        string initiatorControllerId,
        string responderControllerId,
        out IObjectManager objectManager,
        out PartyBase initiator,
        out PartyBase responder,
        out string error)
    {
        objectManager = null;
        initiator = null;
        responder = null;
        error = null;

        if (initiatorControllerId == responderControllerId)
        {
            error = "The PvP fixture requires two different controller ids.";
            return false;
        }

        if (!ContainerProvider.TryResolve<IPlayerManager>(out var players) ||
            !ContainerProvider.TryResolve<IObjectManager>(out objectManager))
        {
            error = "Unable to resolve the player or object manager.";
            return false;
        }

        var initiatorPlayer = players.Players.FirstOrDefault(player => player.ControllerId == initiatorControllerId);
        var responderPlayer = players.Players.FirstOrDefault(player => player.ControllerId == responderControllerId);
        if (initiatorPlayer == null || responderPlayer == null)
        {
            error = "Both controller ids must identify registered players.";
            return false;
        }

        if (!objectManager.TryGetObject<MobileParty>(initiatorPlayer.MobilePartyId, out var initiatorMobileParty) ||
            !objectManager.TryGetObject<MobileParty>(responderPlayer.MobilePartyId, out var responderMobileParty))
        {
            error = "Unable to resolve both registered player parties.";
            return false;
        }

        initiator = initiatorMobileParty.Party;
        responder = responderMobileParty.Party;
        return true;
    }

    private static void RestoreFixture(Fixture fixture)
    {
        if (fixture.MapEvent != null)
        {
            if (!fixture.MapEvent.IsFinalized)
                fixture.MapEvent.FinalizeEvent();

            if (HasAttachedParties(fixture))
                RecoverPartiallyFinalizedMapEvent(fixture);

            if (HasAttachedParties(fixture))
                throw new InvalidOperationException("The fixture still has attached parties.");
        }

        RestorePartyState(
            fixture.InitiatorParty.MobileParty,
            fixture.InitiatorWasActive,
            fixture.InitiatorSettlement,
            fixture.InitiatorPosition);
        RestorePartyState(
            fixture.ResponderParty.MobileParty,
            fixture.ResponderWasActive,
            fixture.ResponderSettlement,
            fixture.ResponderPosition);
    }

    private static void PreparePartyForFieldBattle(MobileParty party)
    {
        party.CurrentSettlement = null;
        party.IsActive = true;
        party.Party.SetVisualAsDirty();
    }

    private static void RestorePartyState(
        MobileParty party,
        bool wasActive,
        Settlement settlement,
        CampaignVec2 position)
    {
        party.CurrentSettlement = settlement;
        party.Position = position;
        party.IsActive = wasActive;
        party.Party.SetVisualAsDirty();
    }

    private static bool HasAttachedParties(Fixture fixture) =>
        fixture.InvolvedParties?.Any(party => party?._mapEventSide?.MapEvent == fixture.MapEvent) == true ||
        fixture.MapEvent?.AttackerSide?.Parties.Count > 0 ||
        fixture.MapEvent?.DefenderSide?.Parties.Count > 0;

    private static void RecoverPartiallyFinalizedMapEvent(Fixture fixture)
    {
        foreach (var party in fixture.InvolvedParties ?? Array.Empty<PartyBase>())
        {
            if (party?._mapEventSide?.MapEvent != fixture.MapEvent)
                continue;

            party._mapEventSide = null;
            if (party.MobileParty != null)
                party.MobileParty.EventPositionAdder = TaleWorlds.Library.Vec2.Zero;
            party.SetVisualAsDirty();
        }

        fixture.MapEvent.AttackerSide?.Clear();
        fixture.MapEvent.DefenderSide?.Clear();
        if (HasAttachedParties(fixture))
            throw new InvalidOperationException("The partially finalized fixture still has attached parties.");

        MessageBroker.Instance.Publish(fixture.MapEvent, new MapEventFinalized(fixture.MapEvent));
        MessageBroker.Instance.Publish(fixture.MapEvent, new InstanceDestroyed<MapEvent>(fixture.MapEvent));
    }

    private static bool TryRestoreFixture(Fixture fixture, out string error)
    {
        try
        {
            RestoreFixture(fixture);
            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}
#endif
