using Autofac;
using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.SiegeEvents.Commands;

/// <summary>Stages two connected defender parties inside Danustica for a restorable siege fixture.</summary>
internal static class DefenderSiegeFixtureCommands
{
    private const string SettlementId = "town_ES1";
    private const int ExpectedPlayerCount = 2;

    private static DefenderSiegeFixture pendingCapture;
    private static DefenderSiegeFixture activeFixture;
    private static DefenderSiegeFixture restoredFixture;

    [CommandLineArgumentFunction("defender_fixture_capture", "coop.debug.siege")]
    public static string Capture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return Failure("capture", "Command can only be run on the server.");
        if (!TryGetExpectedControllerIds(args, out string[] expectedControllerIds, out string error))
            return Failure("capture", error);
        if (activeFixture != null || restoredFixture != null)
            return Failure("capture", "A defender fixture lifecycle is already active.");
        if (pendingCapture != null)
        {
            if (!pendingCapture.HasExpectedControllers(expectedControllerIds) ||
                !IsCaptureCurrent(pendingCapture))
            {
                return Failure("capture", "The pending defender fixture capture is stale or belongs to other controllers.");
            }

            return FixtureResult(pendingCapture, "capture", success: true, reason: null);
        }
        if (!TryCreateFixture(expectedControllerIds, out DefenderSiegeFixture fixture, out error))
            return Failure("capture", error);

        pendingCapture = fixture;
        return FixtureResult(fixture, "capture", success: true, reason: null);
    }

    [CommandLineArgumentFunction("defender_fixture_stage", "coop.debug.siege")]
    public static string Stage(List<string> args)
    {
        if (!ModInformation.IsServer)
            return Failure("stage", "Command can only be run on the server.");
        if (args.Count != 0)
            return Failure("stage", "Usage: coop.debug.siege.defender_fixture_stage");
        if (activeFixture != null)
            return Failure("stage", "The defender fixture is already active; restore it before staging again.");
        if (pendingCapture == null)
            return Failure("stage", "No defender fixture capture is waiting to be staged.");
        if (!IsCaptureCurrent(pendingCapture))
            return Failure("stage", "The captured defender fixture changed before staging.");
        if (!CanStage(pendingCapture, out string error))
            return FixtureResult(pendingCapture, "stage", success: false, reason: error);

        DefenderSiegeFixture fixture = pendingCapture;
        pendingCapture = null;
        activeFixture = fixture;
        try
        {
            foreach (DefenderFixtureParty party in fixture.Parties)
                StagePartyInsideSettlement(party.Party, fixture.Settlement);

            if (!IsStaged(fixture))
                return FixtureResult(fixture, "stage", success: false,
                    reason: "The defender parties did not reach the required inside-settlement state.");

            fixture.IsStaged = true;
            return FixtureResult(fixture, "stage", success: true, reason: null);
        }
        catch (Exception exception)
        {
            return FixtureResult(fixture, "stage", success: false,
                reason: "Staging threw " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    [CommandLineArgumentFunction("defender_fixture_restore", "coop.debug.siege")]
    public static string Restore(List<string> args)
    {
        if (!ModInformation.IsServer)
            return Failure("restore", "Command can only be run on the server.");
        if (args.Count != 0)
            return Failure("restore", "Usage: coop.debug.siege.defender_fixture_restore");
        if (restoredFixture != null)
            return Failure("restore", "A defender fixture is already awaiting restore verification.");
        if (activeFixture == null)
        {
            if (pendingCapture == null)
                return Failure("restore", "No defender fixture is active.");
            if (!IsCaptureCurrent(pendingCapture))
                return Failure("restore", "The un-staged defender fixture capture is no longer clean.");

            restoredFixture = pendingCapture;
            pendingCapture = null;
            return FixtureResult(restoredFixture, "restore", success: true,
                reason: "The captured fixture was never mutated.");
        }
        if (!CanRestore(activeFixture, out string error))
            return FixtureResult(activeFixture, "restore", success: false, reason: error);
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return FixtureResult(activeFixture, "restore", success: false,
                reason: "The mobile-party behavior snapshot service is unavailable.");

        DefenderSiegeFixture fixture = activeFixture;
        try
        {
            foreach (DefenderFixtureParty party in fixture.Parties)
            {
                if (!RestoreParty(party, behaviorSnapshot))
                {
                    return FixtureResult(fixture, "restore", success: false,
                        reason: "The original behavior could not be restored for " + party.ControllerId + ".");
                }
            }

            if (!IsRestored(fixture))
                return FixtureResult(fixture, "restore", success: false,
                    reason: "The defender fixture did not return to its captured state.");

            activeFixture = null;
            restoredFixture = fixture;
            return FixtureResult(fixture, "restore", success: true, reason: null);
        }
        catch (Exception exception)
        {
            return FixtureResult(fixture, "restore", success: false,
                reason: "Restore threw " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    [CommandLineArgumentFunction("defender_fixture_verify_restore", "coop.debug.siege")]
    public static string VerifyRestore(List<string> args)
    {
        if (!ModInformation.IsServer)
            return Failure("verify-restore", "Command can only be run on the server.");
        if (args.Count != 0)
            return Failure("verify-restore", "Usage: coop.debug.siege.defender_fixture_verify_restore");
        if (restoredFixture == null)
            return Failure("verify-restore", "No restored defender fixture is awaiting verification.");

        DefenderSiegeFixture fixture = restoredFixture;
        bool restored = IsRestored(fixture);
        if (restored)
            restoredFixture = null;
        return FixtureResult(fixture, "verify-restore", restored,
            restored ? null : "The defender fixture no longer matches the captured state.");
    }

    [CommandLineArgumentFunction("defender_preassault_ack", "coop.debug.siege")]
    public static string PreAssaultAcknowledgement(List<string> args)
    {
        if (!TryGetExpectedControllerIds(args, out string[] expectedControllerIds, out string error))
            return PreAssaultResult(expectedControllerIds: null, success: false, reason: error,
                settlement: null, playerManager: null);
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            return PreAssaultResult(expectedControllerIds, success: false,
                reason: "The player or object registry is unavailable.", settlement: null,
                playerManager: null);
        }
        if (!objectManager.TryGetObject<Settlement>(SettlementId, out var settlement))
        {
            return PreAssaultResult(expectedControllerIds, success: false,
                reason: "Danustica is unavailable from the object registry.", settlement: null,
                playerManager: playerManager);
        }

        Player[] expectedPlayers = expectedControllerIds
            .Select(controllerId => playerManager.TryGetPlayer(controllerId, out Player player)
                ? player
                : null)
            .ToArray();
        MobileParty[] expectedParties = expectedPlayers
            .Select(player => player != null &&
                objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out MobileParty party)
                    ? party
                : null)
            .ToArray();
        bool expectedPlayersResolved = expectedPlayers.All(player => player != null) &&
            expectedParties.All(party => party != null);
        string[] registeredControllerIds = playerManager.Players
            .Select(player => player.ControllerId)
            .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
            .ToArray();

        bool isServer = ModInformation.IsServer;
        int connectedPlayerCount;
        string[] connectedControllerIds;
        string connectedControllerIdsSource;
        bool connectedControllersExact;
        if (isServer)
        {
            connectedControllerIds = playerManager.Players
                .Where(playerManager.IsConnected)
                .Select(player => player.ControllerId)
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                .ToArray();
            connectedPlayerCount = connectedControllerIds.Length;
            connectedControllerIdsSource = "server-peer-registry";
            connectedControllersExact = DefenderSiegeFixtureContract.HasExactControllerIds(
                connectedControllerIds, expectedControllerIds);
        }
        else if (ContainerProvider.TryResolve<IConnectedPlayerCountService>(out var connectedPlayerCountService))
        {
            connectedPlayerCount = connectedPlayerCountService.ConnectedPlayers;
            connectedControllerIds = Array.Empty<string>();
            connectedControllerIdsSource = "unavailable-on-client";
            connectedControllersExact = false;
        }
        else
        {
            connectedPlayerCount = -1;
            connectedControllerIds = Array.Empty<string>();
            connectedControllerIdsSource = "unavailable";
            connectedControllersExact = false;
        }

        string localControllerId = null;
        MobileParty localParty = null;
        if (!isServer)
        {
            ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
            localControllerId = controllerIdProvider?.ControllerId;
            localParty = MobileParty.MainParty;
        }
        string localPartyId = localParty != null && objectManager.TryGetId(localParty, out string resolvedLocalPartyId)
            ? resolvedLocalPartyId
            : null;
        bool localPartyReady = isServer || IsExpectedLocalParty(
            localControllerId, localParty, expectedControllerIds, expectedPlayers, expectedParties);
        bool noMapEvent = settlement.Party.MapEvent == null &&
            expectedParties.All(party => party?.MapEvent == null);
        bool noBesiegerCamp = settlement.SiegeEvent?.BesiegerCamp == null &&
            expectedParties.All(party => party?.BesiegerCamp == null);
        bool insideSettlement = expectedParties.All(party => party?.CurrentSettlement == settlement);
        bool fixtureStaged = !isServer || activeFixture != null &&
            activeFixture.HasExpectedControllers(expectedControllerIds) && IsStaged(activeFixture);
        bool connectionReady = DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer,
            connectedPlayerCount,
            connectedControllersExact);
        bool success = DefenderSiegeFixtureContract.IsPreAssaultReady(
            fixtureStaged,
            connectionReady,
            expectedPlayersResolved,
            noMapEvent,
            noBesiegerCamp,
            insideSettlement,
            localPartyReady);

        string reason = success
            ? null
            : "The defender pre-assault topology is not ready.";
        return JsonResult(new
        {
            success,
            reason,
            role = isServer ? "server" : "client",
            settlementId = settlement.StringId,
            expectedPlayerCount = ExpectedPlayerCount,
            expectedControllerIds,
            registeredPlayerCount = registeredControllerIds.Length,
            registeredControllerIds,
            connectedPlayerCount,
            connectedControllerIds,
            connectedControllerIdsSource,
            connectedIdentityAuthority = isServer ? "server-peer-registry" : "server-only",
            localControllerId,
            localPartyId,
            localPartyStringId = localParty?.StringId,
            noMapEvent,
            noBesiegerCamp,
            insideSettlement,
            fixtureStaged = isServer ? fixtureStaged : (bool?)null,
            parties = expectedControllerIds.Select((controllerId, index) => new
            {
                controllerId,
                partyId = expectedPlayers[index]?.MobilePartyId,
                partyStringId = expectedParties[index]?.StringId,
                currentSettlementId = expectedParties[index]?.CurrentSettlement?.StringId,
                hasMapEvent = expectedParties[index]?.MapEvent != null,
                hasBesiegerCamp = expectedParties[index]?.BesiegerCamp != null
            }).ToArray()
        });
    }

    private static bool TryCreateFixture(
        string[] expectedControllerIds,
        out DefenderSiegeFixture fixture,
        out string error)
    {
        fixture = null;
        error = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            error = "The defender fixture services are unavailable.";
            return false;
        }
        if (!objectManager.TryGetObject<Settlement>(SettlementId, out var settlement) ||
            !settlement.IsFortification)
        {
            error = "Danustica is not available as a fortification.";
            return false;
        }
        if (settlement.Party.MapEvent != null || settlement.SiegeEvent != null)
        {
            error = "Danustica already has a map event or besieger camp.";
            return false;
        }

        string[] connectedControllerIds = playerManager.Players
            .Where(playerManager.IsConnected)
            .Select(player => player.ControllerId)
            .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
            .ToArray();
        if (!DefenderSiegeFixtureContract.HasExactControllerIds(
                connectedControllerIds, expectedControllerIds))
        {
            error = "Exactly the two expected player controllers must be connected before capture.";
            return false;
        }

        var parties = new List<DefenderFixtureParty>(ExpectedPlayerCount);
        foreach (string controllerId in expectedControllerIds)
        {
            if (!playerManager.TryGetPlayer(controllerId, out Player player) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out MobileParty party))
            {
                error = "The player party for " + controllerId + " is unavailable.";
                return false;
            }
            if (!objectManager.TryGetId(party, out string partyId) || partyId != player.MobilePartyId)
            {
                error = "The player party for " + controllerId + " is not registered under its player identity.";
                return false;
            }
            if (WouldUpdateOwnerVisit(party, settlement))
            {
                error = "The player party for " + controllerId +
                    " would mutate Danustica's owner visit timestamp.";
                return false;
            }
            if (party.CurrentSettlement != null &&
                WouldUpdateOwnerVisit(party, party.CurrentSettlement))
            {
                error = "The player party for " + controllerId +
                    " would mutate its original settlement's owner visit timestamp.";
                return false;
            }
            DefenderFixturePartyState state = ReadPartyState(partyId, party);
            if (!DefenderSiegeFixtureContract.IsCleanForCapture(state))
            {
                error = "The player party for " + controllerId + " is not clean for defender staging.";
                return false;
            }
            if (!behaviorSnapshot.TryCreate(party, out PartyBehaviorUpdateData behavior))
            {
                error = "The original movement state for " + controllerId + " could not be captured.";
                return false;
            }

            parties.Add(new DefenderFixtureParty(
                controllerId,
                partyId,
                party,
                party.CurrentSettlement,
                party.LastVisitedSettlement,
                party.Position,
                party.Bearing,
                behavior,
                state));
        }

        fixture = new DefenderSiegeFixture(expectedControllerIds, settlement, parties.ToArray());
        return true;
    }

    private static bool TryGetExpectedControllerIds(
        IReadOnlyCollection<string> args,
        out string[] expectedControllerIds,
        out string error)
    {
        expectedControllerIds = null;
        error = null;
        if (args.Count != ExpectedPlayerCount)
        {
            error = "Usage: coop.debug.siege.defender_fixture_capture <firstControllerId> <secondControllerId>";
            return false;
        }

        expectedControllerIds = args
            .Select(controllerId => controllerId?.Trim())
            .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
            .ToArray();
        if (expectedControllerIds.Any(string.IsNullOrEmpty) ||
            !DefenderSiegeFixtureContract.HasExactControllerIds(
                expectedControllerIds, expectedControllerIds))
        {
            error = "The two expected controller ids must be non-empty and distinct.";
            expectedControllerIds = null;
            return false;
        }

        return true;
    }

    private static bool IsCaptureCurrent(DefenderSiegeFixture fixture) =>
        fixture.Settlement.Party.MapEvent == null && fixture.Settlement.SiegeEvent == null &&
        fixture.Parties.All(party => DefenderSiegeFixtureContract.IsRestored(
            party.OriginalState, ReadPartyState(party.PartyId, party.Party)));

    private static bool IsStaged(DefenderSiegeFixture fixture) =>
        fixture.Settlement.Party.MapEvent == null && fixture.Settlement.SiegeEvent == null &&
        fixture.Parties.All(party => DefenderSiegeFixtureContract.IsStaged(
            ReadPartyState(party.PartyId, party.Party), fixture.Settlement.StringId));

    private static bool CanStage(DefenderSiegeFixture fixture, out string error)
    {
        error = null;
        if (fixture.Parties.Any(party => WouldUpdateOwnerVisit(party.Party, fixture.Settlement)))
        {
            error = "A defender party would mutate Danustica's owner visit timestamp.";
            return false;
        }
        if (fixture.Parties.Any(party => party.OriginalSettlement != null &&
                WouldUpdateOwnerVisit(party.Party, party.OriginalSettlement)))
        {
            error = "A defender party would mutate its original settlement's owner visit timestamp.";
            return false;
        }

        return true;
    }

    private static bool CanRestore(DefenderSiegeFixture fixture, out string error)
    {
        error = null;
        if (fixture.Settlement.Party.MapEvent != null || fixture.Settlement.SiegeEvent != null)
        {
            error = "Danustica has a map event or besieger camp; fixture restore is unsafe.";
            return false;
        }
        if (fixture.Parties.Any(party => !DefenderSiegeFixtureContract.IsCleanForCapture(
                ReadPartyState(party.PartyId, party.Party))))
        {
            error = "A defender party is no longer clean; fixture restore is unsafe.";
            return false;
        }
        if (fixture.Parties.Any(party => party.OriginalSettlement != null &&
                WouldUpdateOwnerVisit(party.Party, party.OriginalSettlement)))
        {
            error = "A defender party would mutate its original settlement's owner visit timestamp.";
            return false;
        }

        return true;
    }

    private static bool RestoreParty(
        DefenderFixtureParty fixtureParty,
        IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        MobileParty party = fixtureParty.Party;
        if (party.CurrentSettlement != fixtureParty.OriginalSettlement)
        {
            if (party.CurrentSettlement != null)
                LeaveSettlementAction.ApplyForParty(party);
            if (fixtureParty.OriginalSettlement != null)
                EnterSettlementAction.ApplyForParty(party, fixtureParty.OriginalSettlement);
        }

        party.Bearing = fixtureParty.OriginalBearing;
        party.LastVisitedSettlement = fixtureParty.OriginalLastVisitedSettlement;
        if (!behaviorSnapshot.TryApply(party, fixtureParty.OriginalBehavior, out _))
            return false;

        party.Position = fixtureParty.OriginalPosition;
        MessageBroker.Instance.Publish(
            typeof(DefenderSiegeFixtureCommands),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea));
        return true;
    }

    private static void StagePartyInsideSettlement(MobileParty party, Settlement settlement)
    {
        if (party.CurrentSettlement == settlement) return;
        if (party.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(party);
        EnterSettlementAction.ApplyForParty(party, settlement);
    }

    private static bool WouldUpdateOwnerVisit(MobileParty party, Settlement settlement)
    {
        Hero leader = party?.LeaderHero;
        bool partyLeaderIsSettlementOwner = leader != null &&
            leader.Clan == settlement?.OwnerClan && leader.Clan?.Leader == leader;
        return !DefenderSiegeFixtureContract.CanEnterSettlementWithoutOwnerVisit(
            partyLeaderIsSettlementOwner);
    }

    private static bool IsRestored(DefenderSiegeFixture fixture) =>
        fixture.Settlement.Party.MapEvent == null && fixture.Settlement.SiegeEvent == null &&
        fixture.Parties.All(party => DefenderSiegeFixtureContract.IsRestored(
            party.OriginalState, ReadPartyState(party.PartyId, party.Party)));

    private static bool IsExpectedLocalParty(
        string localControllerId,
        MobileParty localParty,
        string[] expectedControllerIds,
        Player[] expectedPlayers,
        MobileParty[] expectedParties)
    {
        if (string.IsNullOrEmpty(localControllerId) || localParty == null) return false;
        int index = Array.IndexOf(expectedControllerIds, localControllerId);
        return index >= 0 && expectedPlayers[index] != null &&
            ReferenceEquals(localParty, expectedParties[index]);
    }

    private static DefenderFixturePartyState ReadPartyState(string partyId, MobileParty party) =>
        new DefenderFixturePartyState(
            partyId,
            party.IsActive,
            party.IsCurrentlyAtSea,
            party.PartyMoveMode == MoveModeType.Hold,
            party.CurrentSettlement?.StringId,
            party.LastVisitedSettlement?.StringId,
            party.Position.X,
            party.Position.Y,
            party.Position.IsOnLand,
            party.Bearing.X,
            party.Bearing.Y,
            party.MapEvent != null,
            party.BesiegerCamp != null,
            party.IsTransitionInProgress,
            party.Army != null,
            party.AttachedTo != null,
            party.AttachedParties?.Count > 0);

    private static string PreAssaultResult(
        string[] expectedControllerIds,
        bool success,
        string reason,
        Settlement settlement,
        IPlayerManager playerManager) => JsonResult(new
        {
            success,
            reason,
            role = ModInformation.IsServer ? "server" : "client",
            settlementId = settlement?.StringId ?? SettlementId,
            expectedPlayerCount = ExpectedPlayerCount,
            expectedControllerIds = expectedControllerIds ?? Array.Empty<string>(),
            registeredPlayerCount = playerManager?.Players.Count ?? 0,
            registeredControllerIds = playerManager?.Players
                .Select(player => player.ControllerId)
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>(),
            connectedPlayerCount = -1,
            connectedControllerIds = Array.Empty<string>(),
            connectedControllerIdsSource = "unavailable",
            connectedIdentityAuthority = ModInformation.IsServer ? "server-peer-registry" : "server-only",
            localControllerId = (string)null,
            localPartyId = (string)null,
            localPartyStringId = (string)null,
            noMapEvent = false,
            noBesiegerCamp = false,
            insideSettlement = false,
            fixtureStaged = (bool?)null,
            parties = Array.Empty<object>()
        });

    private static string FixtureResult(
        DefenderSiegeFixture fixture,
        string phase,
        bool success,
        string reason) => JsonResult(new
        {
            success,
            phase,
            reason,
            settlementId = fixture.Settlement.StringId,
            expectedPlayerCount = ExpectedPlayerCount,
            expectedControllerIds = fixture.ExpectedControllerIds,
            staged = fixture.IsStaged,
            parties = fixture.Parties.Select(party => new
            {
                controllerId = party.ControllerId,
                partyId = party.PartyId,
                partyStringId = party.Party.StringId,
                originalSettlementId = party.OriginalSettlement?.StringId,
                currentSettlementId = party.Party.CurrentSettlement?.StringId,
                hasMapEvent = party.Party.MapEvent != null,
                hasBesiegerCamp = party.Party.BesiegerCamp != null
            }).ToArray()
        });

    private static string Failure(string phase, string reason) => JsonResult(new
    {
        success = false,
        phase,
        reason,
        settlementId = SettlementId
    });

    private static string JsonResult(object value) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(value);

    private sealed class DefenderSiegeFixture
    {
        public string[] ExpectedControllerIds { get; }
        public Settlement Settlement { get; }
        public DefenderFixtureParty[] Parties { get; }
        public bool IsStaged { get; set; }

        public DefenderSiegeFixture(
            string[] expectedControllerIds,
            Settlement settlement,
            DefenderFixtureParty[] parties)
        {
            ExpectedControllerIds = expectedControllerIds;
            Settlement = settlement;
            Parties = parties;
        }

        public bool HasExpectedControllers(IEnumerable<string> controllerIds) =>
            DefenderSiegeFixtureContract.HasExactControllerIds(
                ExpectedControllerIds, controllerIds);
    }

    private sealed class DefenderFixtureParty
    {
        public string ControllerId { get; }
        public string PartyId { get; }
        public MobileParty Party { get; }
        public Settlement OriginalSettlement { get; }
        public Settlement OriginalLastVisitedSettlement { get; }
        public CampaignVec2 OriginalPosition { get; }
        public Vec2 OriginalBearing { get; }
        public PartyBehaviorUpdateData OriginalBehavior { get; }
        public DefenderFixturePartyState OriginalState { get; }

        public DefenderFixtureParty(
            string controllerId,
            string partyId,
            MobileParty party,
            Settlement originalSettlement,
            Settlement originalLastVisitedSettlement,
            CampaignVec2 originalPosition,
            Vec2 originalBearing,
            PartyBehaviorUpdateData originalBehavior,
            DefenderFixturePartyState originalState)
        {
            ControllerId = controllerId;
            PartyId = partyId;
            Party = party;
            OriginalSettlement = originalSettlement;
            OriginalLastVisitedSettlement = originalLastVisitedSettlement;
            OriginalPosition = originalPosition;
            OriginalBearing = originalBearing;
            OriginalBehavior = originalBehavior;
            OriginalState = originalState;
        }
    }
}

internal readonly struct DefenderFixturePartyState : IEquatable<DefenderFixturePartyState>
{
    public string PartyId { get; }
    public bool IsActive { get; }
    public bool IsAtSea { get; }
    public bool IsHolding { get; }
    public string CurrentSettlementId { get; }
    public string LastVisitedSettlementId { get; }
    public float PositionX { get; }
    public float PositionY { get; }
    public bool PositionIsOnLand { get; }
    public float BearingX { get; }
    public float BearingY { get; }
    public bool HasMapEvent { get; }
    public bool HasBesiegerCamp { get; }
    public bool IsTransitionInProgress { get; }
    public bool HasArmy { get; }
    public bool HasAttachedTo { get; }
    public bool HasAttachedParties { get; }

    public DefenderFixturePartyState(
        string partyId,
        bool isActive,
        bool isAtSea,
        bool isHolding,
        string currentSettlementId,
        string lastVisitedSettlementId,
        float positionX,
        float positionY,
        bool positionIsOnLand,
        float bearingX,
        float bearingY,
        bool hasMapEvent,
        bool hasBesiegerCamp,
        bool isTransitionInProgress,
        bool hasArmy,
        bool hasAttachedTo,
        bool hasAttachedParties)
    {
        PartyId = partyId;
        IsActive = isActive;
        IsAtSea = isAtSea;
        IsHolding = isHolding;
        CurrentSettlementId = currentSettlementId;
        LastVisitedSettlementId = lastVisitedSettlementId;
        PositionX = positionX;
        PositionY = positionY;
        PositionIsOnLand = positionIsOnLand;
        BearingX = bearingX;
        BearingY = bearingY;
        HasMapEvent = hasMapEvent;
        HasBesiegerCamp = hasBesiegerCamp;
        IsTransitionInProgress = isTransitionInProgress;
        HasArmy = hasArmy;
        HasAttachedTo = hasAttachedTo;
        HasAttachedParties = hasAttachedParties;
    }

    public bool Equals(DefenderFixturePartyState other) =>
        PartyId == other.PartyId && IsActive == other.IsActive &&
        IsAtSea == other.IsAtSea && IsHolding == other.IsHolding &&
        CurrentSettlementId == other.CurrentSettlementId &&
        LastVisitedSettlementId == other.LastVisitedSettlementId &&
        PositionX.Equals(other.PositionX) && PositionY.Equals(other.PositionY) &&
        PositionIsOnLand == other.PositionIsOnLand &&
        BearingX.Equals(other.BearingX) && BearingY.Equals(other.BearingY) &&
        HasMapEvent == other.HasMapEvent && HasBesiegerCamp == other.HasBesiegerCamp &&
        IsTransitionInProgress == other.IsTransitionInProgress && HasArmy == other.HasArmy &&
        HasAttachedTo == other.HasAttachedTo && HasAttachedParties == other.HasAttachedParties;

    public override bool Equals(object obj) =>
        obj is DefenderFixturePartyState state && Equals(state);

    public override int GetHashCode() =>
        (PartyId ?? string.Empty).GetHashCode();
}

internal static class DefenderSiegeFixtureContract
{
    internal static bool HasExactControllerIds(
        IEnumerable<string> actualControllerIds,
        IEnumerable<string> expectedControllerIds)
    {
        if (actualControllerIds == null || expectedControllerIds == null) return false;
        string[] actual = actualControllerIds.ToArray();
        string[] expected = expectedControllerIds.ToArray();
        if (actual.Length != 2 || expected.Length != 2 ||
            actual.Any(string.IsNullOrEmpty) || expected.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);
        var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
        return actualSet.Count == actual.Length && expectedSet.Count == expected.Length &&
            actualSet.SetEquals(expectedSet);
    }

    internal static bool IsCleanForCapture(DefenderFixturePartyState state) =>
        state.IsActive && !state.IsAtSea && state.IsHolding &&
        !state.HasMapEvent && !state.HasBesiegerCamp &&
        !state.IsTransitionInProgress && !state.HasArmy &&
        !state.HasAttachedTo && !state.HasAttachedParties;

    internal static bool CanEnterSettlementWithoutOwnerVisit(
        bool partyLeaderIsSettlementOwner) => !partyLeaderIsSettlementOwner;

    internal static bool IsStaged(
        DefenderFixturePartyState state,
        string settlementId) =>
        IsCleanForCapture(state) && state.CurrentSettlementId == settlementId;

    internal static bool IsRestored(
        DefenderFixturePartyState expected,
        DefenderFixturePartyState actual) =>
        expected.Equals(actual) && IsCleanForCapture(actual);

    internal static bool IsPreAssaultReady(
        bool fixtureStaged,
        bool connectionReady,
        bool expectedPlayersResolved,
        bool noMapEvent,
        bool noBesiegerCamp,
        bool insideSettlement,
        bool localPartyReady) =>
        fixtureStaged && connectionReady && expectedPlayersResolved &&
        noMapEvent && noBesiegerCamp && insideSettlement && localPartyReady;

    internal static bool IsConnectionReadinessSatisfied(
        bool isServer,
        int connectedPlayerCount,
        bool connectedControllersExact) =>
        connectedPlayerCount == 2 && (!isServer || connectedControllersExact);
}
