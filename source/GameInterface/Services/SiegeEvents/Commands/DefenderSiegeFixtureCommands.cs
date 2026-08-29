using Autofac;
using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyBases.Extensions;
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
using TaleWorlds.CampaignSystem.Roster;
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

#if DEBUG
    private static DefenderRosterFixture rosterFixture;
#endif

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

#if DEBUG
    [CommandLineArgumentFunction("defender_roster_fixture_capture", "coop.debug.siege")]
    public static string CaptureRosterFixture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return RosterFixtureFailure("capture", "Command can only be run on the server.");
        if (!TryGetExpectedControllerIds(args, out string[] expectedControllerIds, out _))
        {
            return RosterFixtureFailure("capture",
                "Usage: coop.debug.siege.defender_roster_fixture_capture <firstControllerId> <secondControllerId>");
        }
        if (rosterFixture != null || pendingCapture != null || activeFixture != null || restoredFixture != null)
        {
            return RosterFixtureFailure("capture",
                "A defender roster or siege fixture lifecycle is already active.");
        }
        if (!TryCreateRosterFixture(expectedControllerIds, out DefenderRosterFixture fixture, out string error))
            return RosterFixtureFailure("capture", error);

        rosterFixture = fixture;
        return RosterFixtureResult(fixture, "capture", success: true, reason: null);
    }

    [CommandLineArgumentFunction("defender_roster_fixture_normalize", "coop.debug.siege")]
    public static string NormalizeRosterFixture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return RosterFixtureFailure("normalize", "Command can only be run on the server.");
        if (args.Count != 0)
            return RosterFixtureFailure("normalize",
                "Usage: coop.debug.siege.defender_roster_fixture_normalize");
        if (rosterFixture == null)
            return RosterFixtureFailure("normalize", "No defender roster fixture is captured.");
        if (rosterFixture.IsNormalized && !rosterFixture.NormalizationUnsafe)
        {
            bool normalizedStateCurrent = IsNormalizedRosterCurrent(rosterFixture, out string normalizedStateError);
            if (DefenderRosterFixtureContract.CanReportNormalizedSuccess(
                    rosterFixture.IsNormalized,
                    rosterFixture.NormalizationUnsafe,
                    normalizedStateCurrent))
            {
                return RosterFixtureResult(rosterFixture, "normalize", success: true, reason: null);
            }

            return RosterFixtureResult(
                rosterFixture,
                "normalize",
                success: false,
                reason: "The normalized defender roster is no longer current: " + normalizedStateError);
        }
        if (!DefenderRosterFixtureContract.IsNormalizationRetryable(
                rosterFixture.IsNormalized,
                rosterFixture.NormalizationUnsafe))
        {

            return RosterFixtureResult(
                rosterFixture,
                "normalize",
                success: false,
                reason: "A prior defender roster normalization left an ambiguous state; automatic fixture actions are disabled until the captured roster is resolved.");
        }
        if (!IsRosterCaptureCurrent(rosterFixture, out string error))
            return RosterFixtureResult(rosterFixture, "normalize", success: false, reason: error);

        var normalized = new List<DefenderRosterFixturePlayer>();
        foreach (DefenderRosterFixturePlayer player in rosterFixture.Players.Where(player => player.WasCaptive))
        {
            try
            {
                EndCaptivityAction.ApplyByEscape(player.Hero);
            }
            catch (Exception exception)
            {
                bool captured = MatchesCapturedCaptivityBaseline(player);
                bool released = IsReleasedForRestoration(player);
                bool releasedStateRestorable = released &&
                    IsReleasedRosterRestorable(rosterFixture, player, out _);
                if (!captured && !releasedStateRestorable)
                {
                    string ambiguousRollbackError = RestoreNormalizedRosterPlayers(
                        rosterFixture,
                        normalized,
                        preserveUnsafeState: true);
                    rosterFixture.IsNormalized = true;
                    rosterFixture.NormalizationUnsafe = true;
                    return RosterFixtureResult(
                        rosterFixture,
                        "normalize",
                        success: false,
                        reason: ambiguousRollbackError == null
                            ? "The authoritative captivity release for " + player.ControllerId + " threw " +
                              exception.GetType().Name + " and left an ambiguous state; prior released players " +
                              "were restored, but automatic recapture was rejected."
                            : "The authoritative captivity release for " + player.ControllerId + " threw " +
                              exception.GetType().Name + " and left an ambiguous state; rollback also failed: " +
                              ambiguousRollbackError);
                }

                if (releasedStateRestorable)
                    normalized.Add(player);
                string releaseRollbackError = RestoreNormalizedRosterPlayers(rosterFixture, normalized);
                return RosterFixtureResult(
                    rosterFixture,
                    "normalize",
                    success: false,
                    reason: releaseRollbackError == null
                        ? "The authoritative captivity release for " + player.ControllerId + " threw " +
                          exception.GetType().Name + "; the captured baseline was restored."
                        : "The authoritative captivity release for " + player.ControllerId + " threw " +
                          exception.GetType().Name + "; rollback also failed: " + releaseRollbackError);
            }

            bool releasedForReadiness = IsReleasedForReadiness(player);
            bool releasedForRestoration = IsReleasedForRestoration(player);
            string restorationError = null;
            bool releasedRestorable = releasedForRestoration &&
                IsReleasedRosterRestorable(rosterFixture, player, out restorationError);
            if (DefenderRosterFixtureContract.CanAcceptNormalizedPlayer(
                    releasedForReadiness,
                    releasedRestorable))
            {
                normalized.Add(player);
                continue;
            }

            bool capturedBaseline = MatchesCapturedCaptivityBaseline(player);
            if (!capturedBaseline && !releasedRestorable)
            {
                string ambiguousReadinessRollbackError = RestoreNormalizedRosterPlayers(
                    rosterFixture,
                    normalized,
                    preserveUnsafeState: true);
                return RosterFixtureResult(
                    rosterFixture,
                    "normalize",
                    success: false,
                    reason: ambiguousReadinessRollbackError == null
                        ? "The authoritative captivity release left " + player.ControllerId +
                          " in a state that cannot be restored safely: " + restorationError +
                          " Prior released players were restored, but automatic fixture actions are disabled."
                        : "The authoritative captivity release left " + player.ControllerId +
                          " in a state that cannot be restored safely: " + restorationError +
                          " Rollback also failed: " + ambiguousReadinessRollbackError);
            }

            if (releasedRestorable)
                normalized.Add(player);
            string readinessRollbackError = RestoreNormalizedRosterPlayers(rosterFixture, normalized);
            return RosterFixtureResult(
                rosterFixture,
                "normalize",
                success: false,
                reason: readinessRollbackError == null
                    ? "The authoritative captivity release did not make " + player.ControllerId +
                      " eligible; the captured baseline was restored."
                    : "The authoritative captivity release did not make " + player.ControllerId +
                      " eligible; rollback also failed: " + readinessRollbackError);

        }

        rosterFixture.IsNormalized = true;
        return RosterFixtureResult(rosterFixture, "normalize", success: true, reason: null);
    }

    [CommandLineArgumentFunction("defender_roster_fixture_restore", "coop.debug.siege")]
    public static string RestoreRosterFixture(List<string> args)
    {
        if (!ModInformation.IsServer)
            return RosterFixtureFailure("restore", "Command can only be run on the server.");
        if (args.Count != 0)
            return RosterFixtureFailure("restore",
                "Usage: coop.debug.siege.defender_roster_fixture_restore");
        if (rosterFixture == null)
            return RosterFixtureFailure("restore", "No defender roster fixture is active.");
        if (rosterFixture.NormalizationUnsafe)
        {
            return RosterFixtureResult(
                rosterFixture,
                "restore",
                success: false,
                reason: "The defender roster fixture is in an ambiguous normalization state; automatic restore was rejected.");
        }
        if (!rosterFixture.IsNormalized)
        {
            if (!IsRosterFixtureRestored(rosterFixture, out string unmutatedError))
            {
                return RosterFixtureResult(rosterFixture, "restore", success: false,
                    reason: "The defender roster fixture was not normalized and its captured state changed: " +
                            unmutatedError);
            }

            rosterFixture.RestoredPendingVerification = true;
            return RosterFixtureResult(rosterFixture, "restore", success: true,
                reason: "The defender roster fixture was captured but never normalized.");
        }
        if (pendingCapture != null || activeFixture != null || restoredFixture != null)
        {
            return RosterFixtureResult(rosterFixture, "restore", success: false,
                reason: "Restore the defender siege fixture before restoring the roster fixture.");
        }
        if (!CanRestoreRosterFixture(rosterFixture, out string error))
            return RosterFixtureResult(rosterFixture, "restore", success: false, reason: error);

        foreach (DefenderRosterFixturePlayer player in rosterFixture.Players.Where(player => player.WasCaptive))
        {
            if (MatchesCapturedCaptivityBaseline(player))
            {
                player.RestoreCompleted = true;
                continue;
            }
            if (!TryRestoreCaptivePlayer(rosterFixture, player, out error))
            {
                if (MatchesCapturedCaptivityBaseline(player))
                {
                    player.RestoreCompleted = true;
                    continue;
                }
                bool recapturedSnapshotReplaySafe = IsRecapturedSnapshotReplaySafe(
                    rosterFixture,
                    player,
                    out _);
                bool releasedForRestoration = IsReleasedRosterRestorable(rosterFixture, player, out _);
                if (DefenderRosterFixtureContract.RequiresRestoreFailureEscalation(
                        capturedBaseline: false,
                        recapturedSnapshotReplaySafe: recapturedSnapshotReplaySafe,
                        releasedForRestoration: releasedForRestoration))
                {
                    rosterFixture.NormalizationUnsafe = true;
                    return RosterFixtureResult(
                        rosterFixture,
                        "restore",
                        success: false,
                        reason: error + " The resulting roster state is ambiguous; automatic fixture actions are disabled.");
                }

                return RosterFixtureResult(rosterFixture, "restore", success: false, reason: error);
            }

            player.RestoreCompleted = true;
        }

        rosterFixture.RestoredPendingVerification = true;
        return RosterFixtureResult(rosterFixture, "restore", success: true, reason: null);
    }

    [CommandLineArgumentFunction("defender_roster_fixture_verify_restore", "coop.debug.siege")]
    public static string VerifyRosterFixtureRestore(List<string> args)
    {
        if (!ModInformation.IsServer)
            return RosterFixtureFailure("verify-restore", "Command can only be run on the server.");
        if (args.Count != 0)
        {
            return RosterFixtureFailure("verify-restore",
                "Usage: coop.debug.siege.defender_roster_fixture_verify_restore");
        }
        if (rosterFixture == null || !rosterFixture.RestoredPendingVerification)
        {
            return RosterFixtureFailure("verify-restore",
                "No restored defender roster fixture is awaiting verification.");
        }

        DefenderRosterFixture fixture = rosterFixture;
        bool restored = IsRosterFixtureRestored(fixture, out string error);
        if (restored)
            rosterFixture = null;
        return RosterFixtureResult(fixture, "verify-restore", restored, error);
    }
#endif

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

#if DEBUG
    private static bool TryCreateRosterFixture(
        string[] expectedControllerIds,
        out DefenderRosterFixture fixture,
        out string error)
    {
        fixture = null;
        error = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            error = "The defender roster fixture services are unavailable.";
            return false;
        }

        if (!HasExpectedConnectedControllers(playerManager, expectedControllerIds))
        {
            error = "Exactly the two expected player controllers must be connected before roster capture.";
            return false;
        }

        var players = new List<DefenderRosterFixturePlayer>(ExpectedPlayerCount);
        foreach (string controllerId in expectedControllerIds)
        {
            if (!playerManager.TryGetPlayer(controllerId, out Player player) ||
                !objectManager.TryGetObject<Hero>(player.HeroId, out Hero hero) ||
                !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out MobileParty party) ||
                !objectManager.TryGetId(party, out string partyId) || partyId != player.MobilePartyId)
            {
                error = "The registered player, hero, or party for " + controllerId + " is unavailable.";
                return false;
            }

            bool wasCaptive = hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null;
            if (wasCaptive && (!hero.IsPrisoner || hero.PartyBelongedToAsPrisoner == null))
            {
                error = "The captivity state for " + controllerId + " is ambiguous.";
                return false;
            }

            PartyBase captorParty = hero.PartyBelongedToAsPrisoner;
            bool hasVisual = party.Party.GetPartyVisual() != null;
            DefenderFixturePartyState partyState = ReadPartyState(partyId, party);
            if (wasCaptive)
            {
                if (!TryGetCaptorHeroPrisonerElement(captorParty, hero, out TroopRosterElement captorHeroElement))
                {
                    error = "The captive player hero for " + controllerId +
                        " is absent from its captor's prisoner roster.";
                    return false;
                }

                if (!DefenderRosterFixtureContract.IsCaptiveBaselineRestorable(
                        hero.IsPrisoner,
                        hero.PartyBelongedToAsPrisoner != null,
                        captorParty.IsActive,
                        hero.PartyBelongedTo != null,
                        hero.HeroState == Hero.CharacterStates.Prisoner,
                        captorHeroElement.Number,
                        captorHeroElement.WoundedNumber,
                        captorHeroElement.Xp,
                        party.IsActive,
                        party.IsVisible,
                        hasVisual,
                        party.LeaderHero != null,
                        party.MemberRoster.GetTroopCount(hero.CharacterObject),
                        party.MemberRoster.TotalManCount,
                        party.PrisonRoster.TotalManCount,
                        party.CurrentSettlement != null,
                        party.MapEvent != null,
                        party.BesiegerCamp != null,
                        party.IsTransitionInProgress,
                        party.Army != null,
                        party.AttachedTo != null,
                        party.AttachedParties?.Count > 0,
                        party.IsCurrentlyAtSea,
                        hero.StayingInSettlement != null))
                {
                    error = "The captive player party for " + controllerId +
                        " is not in a normal restorable captivity state.";
                    return false;
                }
                if (!behaviorSnapshot.TryCreate(party, out PartyBehaviorUpdateData behavior))
                {
                    error = "The original movement state for captive player " + controllerId +
                        " could not be captured.";
                    return false;
                }

                AttackProtectionSnapshot[] protections = CaptureAttackProtections(party);
                FactionAttackProtectionSnapshot[] factionProtections = CaptureFactionAttackProtections(party);
                if (protections.Any(protection =>
                        ReferenceEquals(protection.AttackerParty, captorParty.MobileParty) &&
                        ReferenceEquals(protection.TargetParty, party)))
                {
                    error = "The captive player party for " + controllerId +
                        " already has a former-captor attack protection.";
                    return false;
                }

                players.Add(new DefenderRosterFixturePlayer(
                    controllerId,
                    player,
                    hero,
                    partyId,
                    party,
                    captorParty,
                    hero.PartyBelongedTo,
                    party.LeaderHero,
                    partyState,
                    party.LastVisitedSettlement,
                    party.Bearing,
                    behavior,
                    hero.HeroState,
                    hero.CaptivityStartTime,
                    party._ignoredUntilTime,
                    party.IsVisible,
                    party.IsInspected,
                    hasVisual,
                    party.MemberRoster.GetTroopCount(hero.CharacterObject),
                    party.MemberRoster.TotalManCount,
                    party.PrisonRoster.TotalManCount,
                    protections,
                    factionProtections,
                    wasCaptive: true));
                continue;
            }

            if (!DefenderRosterFixtureContract.IsUncapturedPlayerReady(
                    hero.IsPrisoner,
                    hero.PartyBelongedToAsPrisoner != null,
                    ReferenceEquals(hero.PartyBelongedTo, party),
                    hero.HeroState == Hero.CharacterStates.Active,
                    party.IsActive,
                    party.IsVisible,
                    party.Party.GetPartyVisual() != null,
                    ReferenceEquals(party.LeaderHero, hero),
                    party.MapEvent != null,
                    party.BesiegerCamp != null,
                    party.IsTransitionInProgress,
                    party.Army != null,
                    party.AttachedTo != null,
                    party.AttachedParties?.Count > 0,
                    party.IsCurrentlyAtSea))
            {
                error = "The non-captive player party for " + controllerId +
                    " is not ready for defender roster capture.";
                return false;
            }

            players.Add(new DefenderRosterFixturePlayer(
                controllerId,
                player,
                hero,
                partyId,
                party,
                null,
                hero.PartyBelongedTo,
                party.LeaderHero,
                partyState,
                party.LastVisitedSettlement,
                party.Bearing,
                default,
                hero.HeroState,
                hero.CaptivityStartTime,
                party._ignoredUntilTime,
                party.IsVisible,
                party.IsInspected,
                hasVisual,
                party.MemberRoster.GetTroopCount(hero.CharacterObject),
                party.MemberRoster.TotalManCount,
                party.PrisonRoster.TotalManCount,
                CaptureAttackProtections(party),
                CaptureFactionAttackProtections(party),
                wasCaptive: false));
        }

        if (!players.Any(player => player.WasCaptive))
        {
            error = "The selected roster has no captive player to normalize.";
            return false;
        }

        fixture = new DefenderRosterFixture(expectedControllerIds, players.ToArray(), behaviorSnapshot);
        return true;
    }

    private static bool HasExpectedConnectedControllers(
        IPlayerManager playerManager,
        IEnumerable<string> expectedControllerIds) =>
        DefenderSiegeFixtureContract.HasExactControllerIds(
            playerManager.Players
                .Where(playerManager.IsConnected)
                .Select(player => player.ControllerId)
                .OrderBy(controllerId => controllerId, StringComparer.Ordinal),
            expectedControllerIds);

    private static bool IsRosterCaptureCurrent(DefenderRosterFixture fixture, out string error)
    {
        if (!HasCurrentRosterIdentities(fixture, out error))
            return false;

        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            if (player.WasCaptive && !MatchesCapturedCaptivityBaseline(player))
            {
                error = "The captive baseline for " + player.ControllerId + " changed before normalization.";
                return false;
            }
            if (!player.WasCaptive && !MatchesUncapturedBaseline(player))
            {
                error = "The non-captive baseline for " + player.ControllerId + " changed before normalization.";
                return false;
            }
        }

        return true;
    }

    private static bool IsNormalizedRosterCurrent(DefenderRosterFixture fixture, out string error)
    {
        if (!HasCurrentRosterIdentities(fixture, out error))
            return false;

        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            if (player.WasCaptive && !IsReleasedRosterRestorable(fixture, player, out error))
            {
                return false;
            }
            if (!player.WasCaptive && !MatchesUncapturedBaseline(player))
            {
                error = "The non-captive baseline for " + player.ControllerId + " changed during normalization.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool HasCurrentRosterIdentities(DefenderRosterFixture fixture, out string error)
    {
        error = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            error = "The defender roster fixture services are unavailable.";
            return false;
        }
        if (!HasExpectedConnectedControllers(playerManager, fixture.ExpectedControllerIds))
        {
            error = "The captured defender roster no longer has exactly the expected connected players.";
            return false;
        }

        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            if (!playerManager.TryGetPlayer(player.ControllerId, out Player currentPlayer) ||
                !ReferenceEquals(currentPlayer, player.Player) ||
                !objectManager.TryGetObject<Hero>(currentPlayer.HeroId, out Hero currentHero) ||
                !ReferenceEquals(currentHero, player.Hero) ||
                !objectManager.TryGetObject<MobileParty>(currentPlayer.MobilePartyId, out MobileParty currentParty) ||
                !ReferenceEquals(currentParty, player.Party))
            {
                error = "The captured player identity for " + player.ControllerId + " changed.";
                return false;
            }
        }

        return true;
    }

    private static bool CanRestoreRosterFixture(DefenderRosterFixture fixture, out string error)
    {
        error = null;
        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            if (!player.WasCaptive)
            {
                if (!MatchesUncapturedBaseline(player))
                {
                    error = "The non-captive player baseline for " + player.ControllerId + " changed.";
                    return false;
                }

                continue;
            }

            bool capturedBaseline = MatchesCapturedCaptivityBaseline(player);
            bool recapturedSnapshotReplaySafe = IsRecapturedSnapshotReplaySafe(
                fixture,
                player,
                out string recapturedError);
            bool releasedForRestoration = IsReleasedRosterRestorable(fixture, player, out string releasedError);
            if (!DefenderRosterFixtureContract.CanRestoreCapturedPlayer(
                    capturedBaseline,
                    recapturedSnapshotReplaySafe,
                    releasedForRestoration))
            {
                error = MatchesNormalRecapture(player) ? recapturedError : releasedError;
                return false;
            }
            if (capturedBaseline || recapturedSnapshotReplaySafe)
                continue;
        }

        return true;
    }

    private static bool IsReleasedRosterRestorable(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player,
        out string error)
    {
        bool releasedForRestoration = IsReleasedForRestoration(player);
        bool behaviorRestorable = releasedForRestoration &&
            fixture.BehaviorSnapshot.CanApply(player.Party, player.OriginalBehavior);
        bool attackProtectionsCurrent = releasedForRestoration &&
            HasOnlyCapturedAndReleaseAttackProtections(player);
        if (DefenderRosterFixtureContract.IsReleasedRosterRestorable(
                releasedForRestoration,
                behaviorRestorable,
                attackProtectionsCurrent))
        {
            error = null;
            return true;
        }

        if (!releasedForRestoration)
        {
            error = "The released player party for " + player.ControllerId +
                " is not safe to return through the authoritative captivity action.";
            return false;
        }
        if (!behaviorRestorable)
        {
            error = "The original movement state for " + player.ControllerId +
                " cannot be restored safely.";
            return false;
        }

        error = "The former-captor attack protections for " + player.ControllerId +
            " changed during the fixture.";
        return false;
    }

    private static bool IsRecapturedSnapshotReplaySafe(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player,
        out string error)
    {
        bool normallyRecaptured = MatchesNormalRecapture(player);
        bool behaviorRestorable = normallyRecaptured &&
            fixture.BehaviorSnapshot.CanApply(player.Party, player.OriginalBehavior);
        bool attackProtectionsCurrent = normallyRecaptured &&
            HasOnlyCapturedAndReleaseAttackProtections(player);
        if (DefenderRosterFixtureContract.IsRecapturedSnapshotReplaySafe(
                normallyRecaptured,
                behaviorRestorable,
                attackProtectionsCurrent))
        {
            error = null;
            return true;
        }

        if (!normallyRecaptured)
        {
            error = "The authoritative captivity restore did not recreate the captured state for " +
                player.ControllerId + ".";
            return false;
        }
        if (!behaviorRestorable)
        {
            error = "The original movement state for " + player.ControllerId +
                " cannot be restored safely.";
            return false;
        }

        error = "The former-captor attack protections for " + player.ControllerId +
            " changed during the fixture.";
        return false;
    }

    private static bool IsReleasedForReadiness(DefenderRosterFixturePlayer player) =>
        DefenderRosterFixtureContract.IsReleasedForReadiness(
            player.Hero.IsPrisoner,
            player.Hero.PartyBelongedToAsPrisoner != null,
            ReferenceEquals(player.Hero.PartyBelongedTo, player.Party),
            player.Hero.HeroState == Hero.CharacterStates.Active,
            player.Party.IsActive,
            player.Party.IsVisible,
            player.Party.Party.GetPartyVisual() != null,
            ReferenceEquals(player.Party.LeaderHero, player.Hero),
            player.Party.MemberRoster.GetTroopCount(player.Hero.CharacterObject));

    private static bool IsReleasedForRestoration(DefenderRosterFixturePlayer player) =>
        DefenderRosterFixtureContract.IsReleasedForRestoration(
            player.Hero.IsPrisoner,
            player.Hero.PartyBelongedToAsPrisoner != null,
            ReferenceEquals(player.Hero.PartyBelongedTo, player.Party),
            player.Hero.HeroState == Hero.CharacterStates.Active,
            player.Party.IsActive,
            player.Party.IsVisible,
            player.Party.Party.GetPartyVisual() != null,
            ReferenceEquals(player.Party.LeaderHero, player.Hero),
            player.Party.CurrentSettlement != null,
            player.Party.MapEvent != null,
            player.Party.BesiegerCamp != null,
            player.Party.IsTransitionInProgress,
            player.Party.Army != null,
            player.Party.AttachedTo != null,
            player.Party.AttachedParties?.Count > 0,
            player.Party.IsCurrentlyAtSea,
            player.Party.MemberRoster.GetTroopCount(player.Hero.CharacterObject),
            player.Party.MemberRoster.TotalManCount,
            player.Party.PrisonRoster.TotalManCount,
            player.CaptorParty.IsActive,
            player.CaptorParty.PrisonRoster.GetTroopCount(player.Hero.CharacterObject));

    private static string RestoreNormalizedRosterPlayers(
        DefenderRosterFixture fixture,
        IEnumerable<DefenderRosterFixturePlayer> normalizedPlayers,
        bool preserveUnsafeState = false)
    {
        var errors = new List<string>();
        bool normalizationUnsafe = preserveUnsafeState;
        foreach (DefenderRosterFixturePlayer player in normalizedPlayers.Reverse())
        {
            if (!TryRestoreCaptivePlayer(fixture, player, out string error))
            {
                errors.Add(error);
                bool recapturedSnapshotReplaySafe = IsRecapturedSnapshotReplaySafe(fixture, player, out _);
                bool releasedForRestoration = IsReleasedRosterRestorable(fixture, player, out _);
                if (!DefenderRosterFixtureContract.CanRestoreCapturedPlayer(
                        capturedBaseline: false,
                        recapturedSnapshotReplaySafe: recapturedSnapshotReplaySafe,
                        releasedForRestoration: releasedForRestoration))
                {
                    normalizationUnsafe = true;
                }
            }
        }

        fixture.IsNormalized = preserveUnsafeState || errors.Count > 0;
        fixture.NormalizationUnsafe = normalizationUnsafe;
        return errors.Count == 0 ? null : string.Join(" ", errors);
    }

    private static bool TryRestoreCaptivePlayer(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player,
        out string error)
    {
        error = null;
        if (MatchesCapturedCaptivityBaseline(player))
            return true;

        if (!IsRecapturedSnapshotReplaySafe(fixture, player, out string recapturedError))
        {
            if (!IsReleasedRosterRestorable(fixture, player, out string releasedError))
            {
                error = MatchesNormalRecapture(player) ? recapturedError : releasedError;
                return false;
            }

            try
            {
                TakePrisonerAction.Apply(player.CaptorParty, player.Hero);
            }
            catch (Exception exception)
            {
                error = "The authoritative captivity restore for " + player.ControllerId + " threw " +
                    exception.GetType().Name + ": " + exception.Message;
                return false;
            }

            if (!RestoreCapturedPartyVisibility(player, out error) &&
                !IsRecapturedSnapshotReplaySafe(fixture, player, out _))
            {
                return false;
            }

            if (!IsRecapturedSnapshotReplaySafe(fixture, player, out error))
                return false;
        }

        return TryRestoreRecapturedPartySnapshot(fixture, player, out error);
    }

    private static bool TryRestoreRecapturedPartySnapshot(
        DefenderRosterFixture fixture,
        DefenderRosterFixturePlayer player,
        out string error)
    {
        error = null;
        try
        {
            player.Hero.CaptivityStartTime = player.OriginalCaptivityStartTime;
            player.Party._ignoredUntilTime = player.OriginalIgnoredUntilTime;
            player.Party.IsInspected = player.OriginalIsInspected;
            player.Party.LastVisitedSettlement = player.OriginalLastVisitedSettlement;
            player.Party.Bearing = player.OriginalBearing;
            if (!fixture.BehaviorSnapshot.TryApply(player.Party, player.OriginalBehavior, out _))
            {
                error = "The original movement state for " + player.ControllerId +
                    " could not be reapplied.";
                return false;
            }

            player.Party.Bearing = player.OriginalBearing;
            MessageBroker.Instance.Publish(
                typeof(DefenderSiegeFixtureCommands),
                new PartyBehaviorChangeAttempted(
                    player.Party,
                    forcePosition: true,
                    isCurrentlyAtSea: player.Party.IsCurrentlyAtSea));
            RestoreAttackProtections(player);
            return true;
        }
        catch (Exception exception)
        {
            error = "The captured party state for " + player.ControllerId + " could not be restored: " +
                exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private static bool RestoreCapturedPartyVisibility(DefenderRosterFixturePlayer player, out string error)
    {
        error = null;
        try
        {
            player.Party.IsVisible = player.OriginalIsVisible;
        }
        catch (Exception exception)
        {
            error = "The authoritative captivity restore could not restore captured visibility for " +
                player.ControllerId + ": " + exception.GetType().Name + ": " + exception.Message;
            return false;
        }

        if (player.Party.IsVisible == player.OriginalIsVisible)
            return true;

        error = "The authoritative captivity restore did not restore captured visibility for " +
            player.ControllerId + ".";
        return false;
    }

    private static bool MatchesNormalRecapture(DefenderRosterFixturePlayer player) =>
        TryGetCaptorHeroPrisonerElement(player.CaptorParty, player.Hero, out TroopRosterElement captorHeroElement) &&
        DefenderRosterFixtureContract.IsCaptiveBaselineRestorable(
            player.Hero.IsPrisoner,
            ReferenceEquals(player.Hero.PartyBelongedToAsPrisoner, player.CaptorParty),
            player.CaptorParty.IsActive,
            player.Hero.PartyBelongedTo != null,
            player.Hero.HeroState == Hero.CharacterStates.Prisoner,
            captorHeroElement.Number,
            captorHeroElement.WoundedNumber,
            captorHeroElement.Xp,
            player.Party.IsActive,
            player.Party.IsVisible,
            player.Party.Party.GetPartyVisual() != null,
            player.Party.LeaderHero != null,
            player.Party.MemberRoster.GetTroopCount(player.Hero.CharacterObject),
            player.Party.MemberRoster.TotalManCount,
            player.Party.PrisonRoster.TotalManCount,
            player.Party.CurrentSettlement != null,
            player.Party.MapEvent != null,
            player.Party.BesiegerCamp != null,
            player.Party.IsTransitionInProgress,
            player.Party.Army != null,
            player.Party.AttachedTo != null,
            player.Party.AttachedParties?.Count > 0,
            player.Party.IsCurrentlyAtSea,
            player.Hero.StayingInSettlement != null);

    private static bool TryGetCaptorHeroPrisonerElement(
        PartyBase captorParty,
        Hero hero,
        out TroopRosterElement captorHeroElement)
    {
        int index = captorParty.PrisonRoster.FindIndexOfTroop(hero.CharacterObject);
        if (index < 0)
        {
            captorHeroElement = default;
            return false;
        }

        captorHeroElement = captorParty.PrisonRoster.GetElementCopyAtIndex(index);
        return ReferenceEquals(captorHeroElement.Character, hero.CharacterObject);
    }

    private static bool MatchesCapturedCaptivityBaseline(DefenderRosterFixturePlayer player) =>
        MatchesNormalRecapture(player) &&
        ReferenceEquals(player.Hero.PartyBelongedTo, player.OriginalHeroParty) &&
        ReferenceEquals(player.Party.LeaderHero, player.OriginalLeaderHero) &&
        player.Hero.HeroState == player.OriginalHeroState &&
        player.Hero.CaptivityStartTime.NumTicks == player.OriginalCaptivityStartTime.NumTicks &&
        player.Party._ignoredUntilTime.NumTicks == player.OriginalIgnoredUntilTime.NumTicks &&
        player.Party.IsInspected == player.OriginalIsInspected &&
        DefenderRosterFixtureContract.IsCapturedPartyStateCurrent(
            player.OriginalPartyState,
            ReadPartyState(player.PartyId, player.Party),
            player.CaptorParty.Position.X,
            player.CaptorParty.Position.Y,
            player.CaptorParty.Position.IsOnLand) &&
        HasExpectedAttackProtections(player);

    private static bool MatchesUncapturedBaseline(DefenderRosterFixturePlayer player) =>
        DefenderRosterFixtureContract.IsUncapturedPlayerReady(
            player.Hero.IsPrisoner,
            player.Hero.PartyBelongedToAsPrisoner != null,
            ReferenceEquals(player.Hero.PartyBelongedTo, player.Party),
            player.Hero.HeroState == Hero.CharacterStates.Active,
            player.Party.IsActive,
            player.Party.IsVisible,
            player.Party.Party.GetPartyVisual() != null,
            ReferenceEquals(player.Party.LeaderHero, player.Hero),
            player.Party.MapEvent != null,
            player.Party.BesiegerCamp != null,
            player.Party.IsTransitionInProgress,
            player.Party.Army != null,
            player.Party.AttachedTo != null,
            player.Party.AttachedParties?.Count > 0,
            player.Party.IsCurrentlyAtSea) &&
        player.Hero.HeroState == player.OriginalHeroState &&
        ReferenceEquals(player.Hero.PartyBelongedTo, player.OriginalHeroParty) &&
        ReferenceEquals(player.Party.LeaderHero, player.OriginalLeaderHero) &&
        player.Hero.CaptivityStartTime.NumTicks == player.OriginalCaptivityStartTime.NumTicks &&
        player.Party._ignoredUntilTime.NumTicks == player.OriginalIgnoredUntilTime.NumTicks &&
        player.Party.IsVisible == player.OriginalIsVisible &&
        player.Party.IsInspected == player.OriginalIsInspected &&
        (player.Party.Party.GetPartyVisual() != null) == player.OriginalVisualPresent &&
        player.Party.MemberRoster.GetTroopCount(player.Hero.CharacterObject) == player.OriginalHeroMemberCount &&
        player.Party.MemberRoster.TotalManCount == player.OriginalMemberCount &&
        player.Party.PrisonRoster.TotalManCount == player.OriginalPrisonerCount &&
        player.OriginalPartyState.Equals(ReadPartyState(player.PartyId, player.Party)) &&
        HasExpectedAttackProtections(player);

    private static bool IsRosterFixtureRestored(DefenderRosterFixture fixture, out string error)
    {
        foreach (DefenderRosterFixturePlayer player in fixture.Players)
        {
            bool restored = player.WasCaptive
                ? MatchesCapturedCaptivityBaseline(player)
                : MatchesUncapturedBaseline(player);
            if (restored) continue;

            error = "The roster fixture did not restore the captured state for " + player.ControllerId + ".";
            return false;
        }

        error = null;
        return true;
    }

    private static AttackProtectionSnapshot[] CaptureAttackProtections(MobileParty party) =>
        DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections()
            .Where(protection => ReferenceEquals(protection.AttackerParty, party) ||
                ReferenceEquals(protection.TargetParty, party))
            .Select(protection => new AttackProtectionSnapshot(
                protection.AttackerParty,
                protection.TargetParty,
                protection.DisabledUntil))
            .ToArray();

    private static FactionAttackProtectionSnapshot[] CaptureFactionAttackProtections(MobileParty party) =>
        DefaultMobilePartyAIModelPatches.GetPersistedFactionAttackProtections()
            .Where(protection => ReferenceEquals(protection.AttackerParty, party))
            .Select(protection => new FactionAttackProtectionSnapshot(
                protection.AttackerParty,
                protection.TargetFaction,
                protection.DisabledUntil))
            .ToArray();

    private static bool HasOnlyCapturedAndReleaseAttackProtections(DefenderRosterFixturePlayer player)
    {
        AttackProtectionSnapshot[] actual = CaptureAttackProtections(player.Party);
        bool originalPartyProtectionsCurrent = player.OriginalAttackProtections.All(expected =>
            actual.Any(candidate => expected.Equals(candidate)));
        bool onlyOriginalAndReleaseProtections = actual.All(protection =>
            player.OriginalAttackProtections.Any(expected => expected.Equals(protection)) ||
            ReferenceEquals(protection.AttackerParty, player.CaptorParty.MobileParty) &&
            ReferenceEquals(protection.TargetParty, player.Party) && protection.DisabledUntil.IsFuture);
        bool hasAtMostOneReleaseProtection = actual.Count(protection =>
            ReferenceEquals(protection.AttackerParty, player.CaptorParty.MobileParty) &&
            ReferenceEquals(protection.TargetParty, player.Party) && protection.DisabledUntil.IsFuture) <= 1;
        return DefenderRosterFixtureContract.IsCapturedAttackProtectionStateCurrent(
            originalPartyProtectionsCurrent,
            onlyOriginalAndReleaseProtections,
            hasAtMostOneReleaseProtection,
            HasExpectedFactionAttackProtections(player.Party, player.OriginalFactionAttackProtections));
    }

    private static bool HasExpectedAttackProtections(DefenderRosterFixturePlayer player) =>
        DefenderRosterFixtureContract.HasExactAttackProtectionRestoration(
            HasExpectedPartyAttackProtections(player.Party, player.OriginalAttackProtections),
            HasExpectedFactionAttackProtections(player.Party, player.OriginalFactionAttackProtections));

    private static bool HasExpectedPartyAttackProtections(
        MobileParty party,
        AttackProtectionSnapshot[] expected)
    {
        AttackProtectionSnapshot[] actual = CaptureAttackProtections(party);
        return actual.Length == expected.Length && actual.All(candidate => expected.Any(candidate.Equals));
    }

    private static bool HasExpectedFactionAttackProtections(
        MobileParty party,
        FactionAttackProtectionSnapshot[] expected)
    {
        FactionAttackProtectionSnapshot[] actual = CaptureFactionAttackProtections(party);
        return actual.Length == expected.Length && actual.All(candidate => expected.Any(candidate.Equals));
    }

    private static void RestoreAttackProtections(DefenderRosterFixturePlayer player)
    {
        DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(player.Party);
        foreach (AttackProtectionSnapshot protection in player.OriginalAttackProtections)
        {
            DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                protection.AttackerParty,
                protection.TargetParty,
                protection.DisabledUntil);
        }
        foreach (FactionAttackProtectionSnapshot protection in player.OriginalFactionAttackProtections)
        {
            DefaultMobilePartyAIModelPatches.PreventFactionAttacksUntil(
                protection.AttackerParty,
                protection.TargetFaction,
                protection.DisabledUntil);
        }
    }
#endif

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

#if DEBUG
    private static string RosterFixtureFailure(string phase, string reason) => JsonResult(new
    {
        success = false,
        phase,
        reason,
        expectedPlayerCount = ExpectedPlayerCount,
        requiresReadinessRecheck = true
    });

    private static string RosterFixtureResult(
        DefenderRosterFixture fixture,
        string phase,
        bool success,
        string reason) => JsonResult(new
    {
        success,
        phase,
        reason,
        expectedPlayerCount = ExpectedPlayerCount,
        expectedControllerIds = fixture.ExpectedControllerIds,
        normalized = fixture.IsNormalized,
        normalizationUnsafe = fixture.NormalizationUnsafe,
        restoredPendingVerification = fixture.RestoredPendingVerification,
        requiresReadinessRecheck = true,
        players = fixture.Players.Select(player => new
        {
            controllerId = player.ControllerId,
            heroId = player.Hero.StringId,
            partyId = player.PartyId,
            partyStringId = player.Party.StringId,
            wasCaptive = player.WasCaptive,
            restoreCompleted = player.RestoreCompleted,
            heroIsPrisoner = player.Hero.IsPrisoner,
            heroBelongsToPrisonerParty = player.Hero.PartyBelongedToAsPrisoner != null,
            partyActive = player.Party.IsActive,
            partyVisible = player.Party.IsVisible,
            captorPartyId = GetPartyId(player.CaptorParty)
        }).ToArray()
    });
#endif

    private static string GetPartyId(PartyBase party) =>
        party?.MobileParty?.StringId ?? party?.Settlement?.StringId ?? "none";

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

#if DEBUG
    private sealed class DefenderRosterFixture
    {
        public string[] ExpectedControllerIds { get; }
        public DefenderRosterFixturePlayer[] Players { get; }
        public IMobilePartyBehaviorSnapshot BehaviorSnapshot { get; }
        public bool IsNormalized { get; set; }
        public bool NormalizationUnsafe { get; set; }
        public bool RestoredPendingVerification { get; set; }

        public DefenderRosterFixture(
            string[] expectedControllerIds,
            DefenderRosterFixturePlayer[] players,
            IMobilePartyBehaviorSnapshot behaviorSnapshot)
        {
            ExpectedControllerIds = expectedControllerIds;
            Players = players;
            BehaviorSnapshot = behaviorSnapshot;
        }
    }

    private sealed class DefenderRosterFixturePlayer
    {
        public string ControllerId { get; }
        public Player Player { get; }
        public Hero Hero { get; }
        public string PartyId { get; }
        public MobileParty Party { get; }
        public PartyBase CaptorParty { get; }
        public MobileParty OriginalHeroParty { get; }
        public Hero OriginalLeaderHero { get; }
        public DefenderFixturePartyState OriginalPartyState { get; }
        public Settlement OriginalLastVisitedSettlement { get; }
        public Vec2 OriginalBearing { get; }
        public PartyBehaviorUpdateData OriginalBehavior { get; }
        public Hero.CharacterStates OriginalHeroState { get; }
        public CampaignTime OriginalCaptivityStartTime { get; }
        public CampaignTime OriginalIgnoredUntilTime { get; }
        public bool OriginalIsVisible { get; }
        public bool OriginalIsInspected { get; }
        public bool OriginalVisualPresent { get; }
        public int OriginalHeroMemberCount { get; }
        public int OriginalMemberCount { get; }
        public int OriginalPrisonerCount { get; }
        public AttackProtectionSnapshot[] OriginalAttackProtections { get; }
        public FactionAttackProtectionSnapshot[] OriginalFactionAttackProtections { get; }
        public bool WasCaptive { get; }
        public bool RestoreCompleted { get; set; }

        public DefenderRosterFixturePlayer(
            string controllerId,
            Player player,
            Hero hero,
            string partyId,
            MobileParty party,
            PartyBase captorParty,
            MobileParty originalHeroParty,
            Hero originalLeaderHero,
            DefenderFixturePartyState originalPartyState,
            Settlement originalLastVisitedSettlement,
            Vec2 originalBearing,
            PartyBehaviorUpdateData originalBehavior,
            Hero.CharacterStates originalHeroState,
            CampaignTime originalCaptivityStartTime,
            CampaignTime originalIgnoredUntilTime,
            bool originalIsVisible,
            bool originalIsInspected,
            bool originalVisualPresent,
            int originalHeroMemberCount,
            int originalMemberCount,
            int originalPrisonerCount,
            AttackProtectionSnapshot[] originalAttackProtections,
            FactionAttackProtectionSnapshot[] originalFactionAttackProtections,
            bool wasCaptive)
        {
            ControllerId = controllerId;
            Player = player;
            Hero = hero;
            PartyId = partyId;
            Party = party;
            CaptorParty = captorParty;
            OriginalHeroParty = originalHeroParty;
            OriginalLeaderHero = originalLeaderHero;
            OriginalPartyState = originalPartyState;
            OriginalLastVisitedSettlement = originalLastVisitedSettlement;
            OriginalBearing = originalBearing;
            OriginalBehavior = originalBehavior;
            OriginalHeroState = originalHeroState;
            OriginalCaptivityStartTime = originalCaptivityStartTime;
            OriginalIgnoredUntilTime = originalIgnoredUntilTime;
            OriginalIsVisible = originalIsVisible;
            OriginalIsInspected = originalIsInspected;
            OriginalVisualPresent = originalVisualPresent;
            OriginalHeroMemberCount = originalHeroMemberCount;
            OriginalMemberCount = originalMemberCount;
            OriginalPrisonerCount = originalPrisonerCount;
            OriginalAttackProtections = originalAttackProtections;
            OriginalFactionAttackProtections = originalFactionAttackProtections;
            WasCaptive = wasCaptive;
        }
    }

    private sealed class AttackProtectionSnapshot : IEquatable<AttackProtectionSnapshot>
    {
        public MobileParty AttackerParty { get; }
        public MobileParty TargetParty { get; }
        public CampaignTime DisabledUntil { get; }

        public AttackProtectionSnapshot(
            MobileParty attackerParty,
            MobileParty targetParty,
            CampaignTime disabledUntil)
        {
            AttackerParty = attackerParty;
            TargetParty = targetParty;
            DisabledUntil = disabledUntil;
        }

        public bool Equals(AttackProtectionSnapshot other) =>
            other != null && ReferenceEquals(AttackerParty, other.AttackerParty) &&
            ReferenceEquals(TargetParty, other.TargetParty) &&
            DisabledUntil.NumTicks == other.DisabledUntil.NumTicks;

        public override bool Equals(object obj) => Equals(obj as AttackProtectionSnapshot);

        public override int GetHashCode() =>
            (AttackerParty?.GetHashCode() ?? 0) ^ (TargetParty?.GetHashCode() ?? 0) ^
            DisabledUntil.NumTicks.GetHashCode();
    }

    private sealed class FactionAttackProtectionSnapshot : IEquatable<FactionAttackProtectionSnapshot>
    {
        public MobileParty AttackerParty { get; }
        public IFaction TargetFaction { get; }
        public CampaignTime DisabledUntil { get; }

        public FactionAttackProtectionSnapshot(
            MobileParty attackerParty,
            IFaction targetFaction,
            CampaignTime disabledUntil)
        {
            AttackerParty = attackerParty;
            TargetFaction = targetFaction;
            DisabledUntil = disabledUntil;
        }

        public bool Equals(FactionAttackProtectionSnapshot other) =>
            other != null && ReferenceEquals(AttackerParty, other.AttackerParty) &&
            ReferenceEquals(TargetFaction, other.TargetFaction) &&
            DisabledUntil.NumTicks == other.DisabledUntil.NumTicks;

        public override bool Equals(object obj) => Equals(obj as FactionAttackProtectionSnapshot);

        public override int GetHashCode() =>
            (AttackerParty?.GetHashCode() ?? 0) ^ (TargetFaction?.GetHashCode() ?? 0) ^
            DisabledUntil.NumTicks.GetHashCode();
    }
#endif
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
        state.IsActive && !state.IsAtSea &&
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

#if DEBUG
internal static class DefenderRosterFixtureContract
{
    internal static bool IsNormalizationRetryable(bool isNormalized, bool normalizationUnsafe) =>
        !isNormalized && !normalizationUnsafe;

    internal static bool CanReportNormalizedSuccess(
        bool isNormalized,
        bool normalizationUnsafe,
        bool normalizedStateCurrent) =>
        isNormalized && !normalizationUnsafe && normalizedStateCurrent;

    internal static bool CanRestoreCapturedPlayer(
        bool capturedBaseline,
        bool recapturedSnapshotReplaySafe,
        bool releasedForRestoration) =>
        capturedBaseline || recapturedSnapshotReplaySafe || releasedForRestoration;

    internal static bool RequiresRestoreFailureEscalation(
        bool capturedBaseline,
        bool recapturedSnapshotReplaySafe,
        bool releasedForRestoration) =>
        !CanRestoreCapturedPlayer(
            capturedBaseline,
            recapturedSnapshotReplaySafe,
            releasedForRestoration);

    internal static bool IsRecapturedSnapshotReplaySafe(
        bool normallyRecaptured,
        bool behaviorRestorable,
        bool attackProtectionsCurrent) =>
        normallyRecaptured && behaviorRestorable && attackProtectionsCurrent;

    internal static bool CanAcceptNormalizedPlayer(
        bool releasedForReadiness,
        bool releasedRestorable) =>
        releasedForReadiness && releasedRestorable;

    internal static bool IsReleasedRosterRestorable(
        bool releasedForRestoration,
        bool behaviorRestorable,
        bool attackProtectionsCurrent) =>
        releasedForRestoration && behaviorRestorable && attackProtectionsCurrent;

    internal static bool HasExactAttackProtectionRestoration(
        bool partyProtectionsRestored,
        bool factionProtectionsRestored) =>
        partyProtectionsRestored && factionProtectionsRestored;

    internal static bool IsCapturedAttackProtectionStateCurrent(
        bool originalPartyProtectionsCurrent,
        bool onlyOriginalAndReleaseProtections,
        bool hasAtMostOneReleaseProtection,
        bool factionProtectionsCurrent) =>
        originalPartyProtectionsCurrent && onlyOriginalAndReleaseProtections &&
        hasAtMostOneReleaseProtection && factionProtectionsCurrent;

    internal static bool IsCapturedPartyStateCurrent(
        DefenderFixturePartyState originalState,
        DefenderFixturePartyState currentState,
        float captorPositionX,
        float captorPositionY,
        bool captorPositionIsOnLand) =>
        originalState.PartyId == currentState.PartyId &&
        originalState.IsActive == currentState.IsActive &&
        originalState.IsAtSea == currentState.IsAtSea &&
        originalState.IsHolding == currentState.IsHolding &&
        originalState.CurrentSettlementId == currentState.CurrentSettlementId &&
        originalState.LastVisitedSettlementId == currentState.LastVisitedSettlementId &&
        currentState.PositionX.Equals(captorPositionX) &&
        currentState.PositionY.Equals(captorPositionY) &&
        currentState.PositionIsOnLand == captorPositionIsOnLand &&
        originalState.BearingX.Equals(currentState.BearingX) &&
        originalState.BearingY.Equals(currentState.BearingY) &&
        originalState.HasMapEvent == currentState.HasMapEvent &&
        originalState.HasBesiegerCamp == currentState.HasBesiegerCamp &&
        originalState.IsTransitionInProgress == currentState.IsTransitionInProgress &&
        originalState.HasArmy == currentState.HasArmy &&
        originalState.HasAttachedTo == currentState.HasAttachedTo &&
        originalState.HasAttachedParties == currentState.HasAttachedParties;

    internal static bool IsCaptiveBaselineRestorable(
        bool heroIsPrisoner,
        bool heroHasCaptor,
        bool captorPartyIsActive,
        bool heroHasParty,
        bool heroStateIsPrisoner,
        int captorHeroCount,
        int captorHeroWoundedNumber,
        int captorHeroXp,
        bool partyActive,
        bool partyVisible,
        bool partyHasVisual,
        bool partyHasLeader,
        int partyHeroMemberCount,
        int partyMemberCount,
        int partyPrisonerCount,
        bool partyHasCurrentSettlement,
        bool partyHasMapEvent,
        bool partyHasBesiegerCamp,
        bool partyIsTransitioning,
        bool partyHasArmy,
        bool partyHasAttachedTo,
        bool partyHasAttachedParties,
        bool partyIsAtSea,
        bool heroStaysInSettlement) =>
        heroIsPrisoner && heroHasCaptor && captorPartyIsActive && !heroHasParty && heroStateIsPrisoner &&
        captorHeroCount == 1 &&
        captorHeroWoundedNumber == 0 && captorHeroXp == 0 &&
        !partyActive && !partyVisible && !partyHasVisual && !partyHasLeader &&
        partyHeroMemberCount == 0 && partyMemberCount == 0 && partyPrisonerCount == 0 &&
        !partyHasCurrentSettlement && !partyHasMapEvent && !partyHasBesiegerCamp &&
        !partyIsTransitioning && !partyHasArmy && !partyHasAttachedTo &&
        !partyHasAttachedParties && !partyIsAtSea && !heroStaysInSettlement;

    internal static bool IsUncapturedPlayerReady(
        bool heroIsPrisoner,
        bool heroHasCaptor,
        bool heroBelongsToPlayerParty,
        bool heroStateIsActive,
        bool partyActive,
        bool partyVisible,
        bool partyHasVisual,
        bool partyLeaderIsHero,
        bool partyHasMapEvent,
        bool partyHasBesiegerCamp,
        bool partyIsTransitioning,
        bool partyHasArmy,
        bool partyHasAttachedTo,
        bool partyHasAttachedParties,
        bool partyIsAtSea) =>
        !heroIsPrisoner && !heroHasCaptor && heroBelongsToPlayerParty && heroStateIsActive && partyActive &&
        partyVisible && partyHasVisual && partyLeaderIsHero &&
        !partyHasMapEvent && !partyHasBesiegerCamp && !partyIsTransitioning &&
        !partyHasArmy && !partyHasAttachedTo && !partyHasAttachedParties && !partyIsAtSea;

    internal static bool IsReleasedForReadiness(
        bool heroIsPrisoner,
        bool heroHasCaptor,
        bool heroBelongsToPlayerParty,
        bool heroStateIsActive,
        bool partyActive,
        bool partyVisible,
        bool partyHasVisual,
        bool partyLeaderIsHero,
        int partyHeroMemberCount) =>
        !heroIsPrisoner && !heroHasCaptor && heroBelongsToPlayerParty && heroStateIsActive && partyActive &&
        partyVisible && partyHasVisual && partyLeaderIsHero && partyHeroMemberCount == 1;

    internal static bool IsReleasedForRestoration(
        bool heroIsPrisoner,
        bool heroHasCaptor,
        bool heroBelongsToPlayerParty,
        bool heroStateIsActive,
        bool partyActive,
        bool partyVisible,
        bool partyHasVisual,
        bool partyLeaderIsHero,
        bool partyHasCurrentSettlement,
        bool partyHasMapEvent,
        bool partyHasBesiegerCamp,
        bool partyIsTransitioning,
        bool partyHasArmy,
        bool partyHasAttachedTo,
        bool partyHasAttachedParties,
        bool partyIsAtSea,
        int partyHeroMemberCount,
        int partyMemberCount,
        int partyPrisonerCount,
        bool captorPartyIsActive,
        int captorHeroPrisonerCount) =>
        !heroIsPrisoner && !heroHasCaptor && heroBelongsToPlayerParty && heroStateIsActive && partyActive &&
        partyVisible && partyHasVisual && partyLeaderIsHero &&
        !partyHasCurrentSettlement && !partyHasMapEvent && !partyHasBesiegerCamp &&
        !partyIsTransitioning && !partyHasArmy && !partyHasAttachedTo &&
        !partyHasAttachedParties && !partyIsAtSea && partyHeroMemberCount == 1 && partyMemberCount == 1 &&
        partyPrisonerCount == 0 && captorPartyIsActive && captorHeroPrisonerCount == 0;
}
#endif
