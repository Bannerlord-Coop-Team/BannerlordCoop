#if DEBUG
using System;
using Common.Commands;
using Common;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections;
using GameInterface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Newtonsoft.Json;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;
using ServerLoadingState = Coop.Core.Server.Connections.States.LoadingState;

namespace Coop.Core.Common.Commands;

/// <summary>
/// Stages and observes campaign-join scenarios in DEBUG builds.
/// </summary>
public static class JoinDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static Func<bool> startClientSession;
    private static int forceNextInactivePartyDeficit;
    private static string desiredInactivePartyId;
    private static string lastForcedPartyId = "none";
    private static MobileParty stagedInactiveParty;
    private static bool stagedInactivePartyWasActive;

    public sealed class JoinStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.connection";

        public string Name => "join_state";

        public string Description => "Reports the current campaign join state.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
            {
                if (!ContainerProvider.TryResolve<IConnectionCollection>(out var connections))
                {
                    return Failed("Failed to get connection collection");
                }

                string connectionState = string.Join(" | ", connections.Select(connection =>
                {
                    string state = connection.State?.GetType().Name ?? "none";
                    string details = connection.State is ServerLoadingState loading
                        ? loading.DebugJoinState
                        : "joinCatchUpPending=false";
                    return $"peer={connection.Peer.Id} state={state} {details}";
                }));
                return Succeeded($"{GetPartyCounts()} | {connectionState}");
            }

            if (!ContainerProvider.TryResolve<IClientLogic>(out var clientLogic))
            {
                return Failed("Failed to get client logic");
            }

            return Succeeded(clientLogic.State is CampaignState campaignState
                ? $"{campaignState.DebugJoinState} {GetPartyCounts()} " +
                  $"forcedInactiveParty={lastForcedPartyId}"
                : $"state={clientLogic.State?.GetType().Name ?? "none"} {GetPartyCounts()} " +
                  $"forcedInactiveParty={lastForcedPartyId}");
        }
    }

    public sealed class PlayerPartyReadinessCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.connection";
        public string Name => "player_party_readiness";
        public string Description => "Reports synchronized defender party readiness.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("controller_id", "The connected player controller id."),
        };
        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) =>
            Succeeded(PlayerPartyReadiness(args.ToList()));
    }

    private static string PlayerPartyReadiness(List<string> args)
    {
        if (!ModInformation.IsServer)
            return PlayerPartyReadinessResult(
                controllerId: null,
                success: false,
                reason: "Command can only be run on the server.",
                playerRegistered: false,
                connected: false,
                peerBound: false,
                currentPeerBinding: false,
                completedSynchronization: false,
                peerId: null,
                partyId: null,
                partyStringId: null,
                partyActive: null,
                heroId: null,
                heroResolved: false,
                heroCaptive: null,
                heroIsPrisoner: null,
                heroBelongsToPrisonerParty: null,
                classification: PlayerPartyReadinessContract.InvalidFixtureRosterUnavailable);
        if (args.Count != 1 || string.IsNullOrWhiteSpace(args[0]))
            return PlayerPartyReadinessResult(
                controllerId: null,
                success: false,
                reason: "Usage: coop.debug.connection.player_party_readiness <controllerId>",
                playerRegistered: false,
                connected: false,
                peerBound: false,
                currentPeerBinding: false,
                completedSynchronization: false,
                peerId: null,
                partyId: null,
                partyStringId: null,
                partyActive: null,
                heroId: null,
                heroResolved: false,
                heroCaptive: null,
                heroIsPrisoner: null,
                heroBelongsToPrisonerParty: null,
                classification: PlayerPartyReadinessContract.InvalidFixtureRosterUnavailable);
        if (!ContainerProvider.TryResolve<IConnectionCollection>(out var connections) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return PlayerPartyReadinessResult(
                controllerId: args[0],
                success: false,
                reason: "The player, connection, or object registry is unavailable.",
                playerRegistered: false,
                connected: false,
                peerBound: false,
                currentPeerBinding: false,
                completedSynchronization: false,
                peerId: null,
                partyId: null,
                partyStringId: null,
                partyActive: null,
                heroId: null,
                heroResolved: false,
                heroCaptive: null,
                heroIsPrisoner: null,
                heroBelongsToPrisonerParty: null,
                classification: PlayerPartyReadinessContract.InvalidFixtureRosterUnavailable);
        }

        string controllerId = args[0];
        bool playerRegistered = playerManager.TryGetPlayer(controllerId, out Player player);
        bool connected = playerRegistered && playerManager.IsConnected(player);
        NetPeer peer = null;
        bool peerBound = playerRegistered && playerManager.TryGetPeer(controllerId, out peer);
        bool currentPeerBinding = peerBound && playerManager.TryGetPlayer(peer, out Player peerPlayer) &&
            ReferenceEquals(player, peerPlayer);
        bool completedSynchronization = currentPeerBinding &&
            connections.HasCompletedCampaignSynchronization(peer);
        MobileParty party = null;
        bool partyResolved = playerRegistered &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out party);
        bool? partyActive = partyResolved ? party.IsActive : null;
        Hero hero = null;
        bool heroResolved = playerRegistered &&
            objectManager.TryGetObject<Hero>(player.HeroId, out hero);
        bool? heroIsPrisoner = heroResolved ? hero.IsPrisoner : null;
        bool? heroBelongsToPrisonerParty = heroResolved
            ? hero.PartyBelongedToAsPrisoner != null
            : null;
        bool? heroCaptive = heroResolved
            ? hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null
            : null;
        string classification = PlayerPartyReadinessContract.Classify(
            playerRegistered,
            connected,
            currentPeerBinding,
            completedSynchronization,
            partyResolved,
            partyActive == true,
            heroResolved,
            heroCaptive == true);
        bool success = classification == PlayerPartyReadinessContract.Eligible;

        return PlayerPartyReadinessResult(
            controllerId,
            success,
            PlayerPartyReadinessContract.GetReason(classification),
            playerRegistered,
            connected,
            peerBound,
            currentPeerBinding,
            completedSynchronization,
            peerBound ? (int?)peer.Id : null,
            playerRegistered ? player.MobilePartyId : null,
            partyResolved ? party.StringId : null,
            partyActive,
            playerRegistered ? player.HeroId : null,
            heroResolved,
            heroCaptive,
            heroIsPrisoner,
            heroBelongsToPrisonerParty,
            classification);
    }

    public sealed class ArmInactivePartyDeficitCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.connection";

        public string Name => "arm_inactive_party_deficit";

        public string Description => "Arms the next client join baseline to omit an inactive party.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party_string_id", "The inactive party StringId."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
            {
                return Failed("The inactive-party deficit fixture is client-only.");
            }

            desiredInactivePartyId = args[0];
            lastForcedPartyId = "armed";
            Interlocked.Exchange(ref forceNextInactivePartyDeficit, 1);
            return Succeeded($"The next complete join baseline will remove inactive party '{args[0]}' " +
                   "from the client campaign collection before validation.");
        }
    }

    public sealed class StageInactivePartyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.connection";

        public string Name => "stage_inactive_party";

        public string Description => "Stages an isolated server party as inactive for join testing.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
            {
                return Failed("stage_inactive_party must be run on the server.");
            }
            if (stagedInactiveParty != null)
            {
                return Succeeded(GetStagedPartyResult(stagedInactiveParty));
            }

            var parties = Campaign.Current?.CampaignObjectManager?.MobileParties;
            stagedInactiveParty = parties?.FirstOrDefault(party =>
                    party?.IsActive == true &&
                    party.Ai != null &&
                    !party.IsPlayerParty() &&
                    party.Army == null &&
                    party.AttachedTo == null &&
                    party.MapEvent == null &&
                    party.CurrentSettlement == null &&
                    party.BesiegedSettlement == null &&
                    party.BesiegerCamp == null &&
                    !party.IsTransitionInProgress &&
                    !party.StartTransitionNextFrameToExitFromPort &&
                    !party.IsInRaftState &&
                    !IsReferencedByActiveParty(party, parties));
            if (stagedInactiveParty == null)
            {
                return Failed("No isolated active non-player field party was available for the fixture.");
            }

            stagedInactivePartyWasActive = stagedInactiveParty.IsActive;
            stagedInactiveParty.IsActive = false;
            return Succeeded(GetStagedPartyResult(stagedInactiveParty));
        }
    }

    public sealed class RestoreInactivePartyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.connection";

        public string Name => "restore_inactive_party";

        public string Description => "Restores the staged inactive server party.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer)
            {
                return Failed("restore_inactive_party must be run on the server.");
            }
            if (stagedInactiveParty == null)
            {
                return Failed("No inactive-party fixture is staged.");
            }

            string partyId = stagedInactiveParty.StringId;
            stagedInactiveParty.IsActive = stagedInactivePartyWasActive;
            bool restoredActive = stagedInactiveParty.IsActive;
            stagedInactiveParty = null;
            stagedInactivePartyWasActive = false;
            return Succeeded($"restoredParty={partyId} active={restoredActive}");
        }
    }

    public sealed class DisconnectCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.connection";

        public string Name => "disconnect";

        public string Description => "Disconnects the active client session.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
            {
                return Failed("disconnect must be run on a client.");
            }
            if (!ContainerProvider.TryResolve<IClientLogic>(out var clientLogic))
            {
                return Failed("No active client session was found.");
            }

            clientLogic.Disconnect();
            return Succeeded("Client session is returning to the main menu.");
        }
    }

    [CommandLineArgumentFunction("reconnect", "coop.debug.connection")]
    public static string Reconnect(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.connection.reconnect";
        }
        if (ModInformation.IsServer)
        {
            return "reconnect must be run on a client.";
        }
        if (ContainerProvider.TryResolve<IClientLogic>(out var clientLogic))
        {
            clientLogic.Connect();
            return "Client session is reconnecting to the configured server.";
        }

        Func<bool> starter = Volatile.Read(ref startClientSession);
        if (starter == null)
            return "The process client-session starter is unavailable.";
        if (!starter())
            throw new InvalidOperationException("Client co-op connection start was refused.");

        return "Client co-op session restarted after teardown.";
    }

    public static void ConfigureClientSessionStarter(Func<bool> starter)
    {
        if (starter == null) throw new ArgumentNullException(nameof(starter));

        Volatile.Write(ref startClientSession, starter);
    }

    internal static void ResetClientSessionStarter()
    {
        Volatile.Write(ref startClientSession, null);
    }

    internal static void ForceArmedInactivePartyDeficit()
    {
        if (Interlocked.Exchange(ref forceNextInactivePartyDeficit, 0) == 0) return;

        var manager = Campaign.Current?.CampaignObjectManager;
        string partyId = desiredInactivePartyId;
        desiredInactivePartyId = null;
        MobileParty candidate = manager?.MobileParties.FirstOrDefault(party =>
            party?.StringId == partyId &&
            !party.IsActive &&
            !ReferenceEquals(party, MobileParty.MainParty));
        if (candidate == null)
        {
            lastForcedPartyId = "missing";
            throw new System.InvalidOperationException(
                $"The armed join fixture could not find inactive non-main party '{partyId}'.");
        }

        lastForcedPartyId = candidate.StringId;
        JoinBaselineFixture.RemoveFromCampaignCollection(manager, candidate);
    }

    private static bool IsReferencedByActiveParty(
        MobileParty candidate,
        IEnumerable<MobileParty> parties)
    {
        foreach (MobileParty party in parties)
        {
            if (party?.IsActive != true || ReferenceEquals(party, candidate)) continue;

            object interactable = party.Ai?.AiBehaviorInteractable;
            if (ReferenceEquals(party.TargetParty, candidate) ||
                ReferenceEquals(party.MoveTargetParty, candidate) ||
                ReferenceEquals(interactable, candidate.Party) ||
                ReferenceEquals(interactable, candidate.Anchor))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPartyCounts()
    {
        var parties = Campaign.Current?.CampaignObjectManager?.MobileParties;
        if (parties == null) return "partyTotal=-1 activeParties=-1 inactiveParties=-1";

        int active = parties.Count(party => party?.IsActive == true);
        return $"partyTotal={parties.Count} activeParties={active} " +
               $"inactiveParties={parties.Count - active}";
    }

    private static string GetStagedPartyResult(MobileParty party)
    {
        string active = party.IsActive ? "true" : "false";
        return $"LIVE_TEST_JSON={{\"partyId\":\"{party.StringId}\",\"active\":{active}}}";
    }
    private static string PlayerPartyReadinessResult(
        string controllerId,
        bool success,
        string reason,
        bool playerRegistered,
        bool connected,
        bool peerBound,
        bool currentPeerBinding,
        bool completedSynchronization,
        int? peerId,
        string partyId,
        string partyStringId,
        bool? partyActive,
        string heroId,
        bool heroResolved,
        bool? heroCaptive,
        bool? heroIsPrisoner,
        bool? heroBelongsToPrisonerParty,
        string classification) =>
        "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            success,
            reason,
            role = ModInformation.IsServer ? "server" : "client",
            controllerId,
            playerRegistered,
            connected,
            peerBound,
            currentPeerBinding,
            completedSynchronization,
            peerId,
            partyId,
            partyStringId,
            partyActive,
            heroId,
            heroResolved,
            heroCaptive,
            heroIsPrisoner,
            heroBelongsToPrisonerParty,
            classification,
            fixtureEligible = classification == PlayerPartyReadinessContract.Eligible,
            currentSynchronizedNonCaptiveInactive =
                classification == PlayerPartyReadinessContract.CurrentSynchronizedNonCaptiveInactive
        });
}
internal static class PlayerPartyReadinessContract
{
    internal const string Eligible = "eligible";
    internal const string InvalidFixtureRosterCaptive = "invalid-fixture-roster-captive";
    internal const string InvalidFixtureRosterUnavailable = "invalid-fixture-roster-unavailable";
    internal const string CurrentSynchronizedNonCaptiveInactive =
        "current-synchronized-noncaptive-inactive";

    internal static string Classify(
        bool playerRegistered,
        bool connected,
        bool currentPeerBinding,
        bool completedSynchronization,
        bool partyResolved,
        bool partyActive,
        bool heroResolved,
        bool heroCaptive)
    {
        if (heroResolved && heroCaptive) return InvalidFixtureRosterCaptive;
        if (!playerRegistered || !connected || !currentPeerBinding ||
            !completedSynchronization || !partyResolved || !heroResolved)
        {
            return InvalidFixtureRosterUnavailable;
        }

        return partyActive ? Eligible : CurrentSynchronizedNonCaptiveInactive;
    }

    internal static string GetReason(string classification) => classification switch
    {
        Eligible => null,
        InvalidFixtureRosterCaptive => "The selected player hero is captive.",
        CurrentSynchronizedNonCaptiveInactive =>
            "The current synchronized non-captive player party remains inactive.",
        _ => "The selected player is unavailable for the defender fixture."
    };
}
#endif
