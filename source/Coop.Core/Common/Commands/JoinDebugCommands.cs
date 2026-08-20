#if DEBUG
using Common;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections;
using GameInterface;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
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
internal static class JoinDebugCommands
{
    private static int forceNextInactivePartyDeficit;
    private static string desiredInactivePartyId;
    private static string lastForcedPartyId = "none";
    private static MobileParty stagedInactiveParty;
    private static bool stagedInactivePartyWasActive;

    [CommandLineArgumentFunction("campaign_readiness", "coop.debug.connection")]
    public static string CampaignReadiness(List<string> args)
    {
        if (args.Count != 2 || args[0] == args[1])
        {
            return CampaignReadinessFailure("Usage: coop.debug.connection.campaign_readiness <firstControllerId> <secondControllerId>");
        }

        if (!ModInformation.IsServer)
        {
            return CampaignReadinessFailure("This command can only be used by the server");
        }

        if (!ContainerProvider.TryResolve<IConnectionCollection>(out var connections)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
        {
            return CampaignReadinessFailure("Unable to resolve campaign readiness services");
        }

        var first = GetCampaignReadiness(args[0], connections, playerManager);
        var second = GetCampaignReadiness(args[1], connections, playerManager);
        bool success = first.Ready && second.Ready;
        return "LIVE_TEST_JSON={\"success\":" + JsonBoolean(success) +
            ",\"first\":" + CampaignReadinessJson(first) +
            ",\"second\":" + CampaignReadinessJson(second) + "}";
    }

    [CommandLineArgumentFunction("join_state", "coop.debug.connection")]
    public static string JoinState(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.connection.join_state";
        }

        if (ModInformation.IsServer)
        {
            if (!ContainerProvider.TryResolve<IConnectionCollection>(out var connections))
            {
                return "Failed to get connection collection";
            }

            string connectionState = string.Join(" | ", connections.Select(connection =>
            {
                string state = connection.State?.GetType().Name ?? "none";
                string details = connection.State is ServerLoadingState loading
                    ? loading.DebugJoinState
                    : "joinCatchUpPending=false";
                return $"peer={connection.Peer.Id} state={state} {details}";
            }));
            return $"{GetPartyCounts()} | {connectionState}";
        }

        if (!ContainerProvider.TryResolve<IClientLogic>(out var clientLogic))
        {
            return "Failed to get client logic";
        }

        return clientLogic.State is CampaignState campaignState
            ? $"{campaignState.DebugJoinState} {GetPartyCounts()} " +
              $"forcedInactiveParty={lastForcedPartyId}"
            : $"state={clientLogic.State?.GetType().Name ?? "none"} {GetPartyCounts()} " +
              $"forcedInactiveParty={lastForcedPartyId}";
    }

    [CommandLineArgumentFunction("arm_inactive_party_deficit", "coop.debug.connection")]
    public static string ArmInactivePartyDeficit(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.connection.arm_inactive_party_deficit <partyStringId>";
        }
        if (ModInformation.IsServer)
        {
            return "The inactive-party deficit fixture is client-only.";
        }

        desiredInactivePartyId = args[0];
        lastForcedPartyId = "armed";
        Interlocked.Exchange(ref forceNextInactivePartyDeficit, 1);
        return $"The next complete join baseline will remove inactive party '{args[0]}' " +
               "from the client campaign collection before validation.";
    }

    [CommandLineArgumentFunction("stage_inactive_party", "coop.debug.connection")]
    public static string StageInactiveParty(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.connection.stage_inactive_party";
        }
        if (!ModInformation.IsServer)
        {
            return "stage_inactive_party must be run on the server.";
        }
        if (stagedInactiveParty != null)
        {
            return GetStagedPartyResult(stagedInactiveParty);
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
            return "No isolated active non-player field party was available for the fixture.";
        }

        stagedInactivePartyWasActive = stagedInactiveParty.IsActive;
        stagedInactiveParty.IsActive = false;
        return GetStagedPartyResult(stagedInactiveParty);
    }

    [CommandLineArgumentFunction("restore_inactive_party", "coop.debug.connection")]
    public static string RestoreInactiveParty(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.connection.restore_inactive_party";
        }
        if (!ModInformation.IsServer)
        {
            return "restore_inactive_party must be run on the server.";
        }
        if (stagedInactiveParty == null)
        {
            return "No inactive-party fixture is staged.";
        }

        string partyId = stagedInactiveParty.StringId;
        stagedInactiveParty.IsActive = stagedInactivePartyWasActive;
        bool restoredActive = stagedInactiveParty.IsActive;
        stagedInactiveParty = null;
        stagedInactivePartyWasActive = false;
        return $"restoredParty={partyId} active={restoredActive}";
    }

    [CommandLineArgumentFunction("disconnect", "coop.debug.connection")]
    public static string Disconnect(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.connection.disconnect";
        }
        if (ModInformation.IsServer)
        {
            return "disconnect must be run on a client.";
        }
        if (!ContainerProvider.TryResolve<IClientLogic>(out var clientLogic))
        {
            return "No active client session was found.";
        }

        clientLogic.Disconnect();
        return "Client session is returning to the main menu.";
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

    private static CampaignReadinessState GetCampaignReadiness(
        string controllerId,
        IConnectionCollection connections,
        IPlayerManager playerManager)
    {
        var state = new CampaignReadinessState
        {
            Registered = playerManager.TryGetPlayer(controllerId, out var player),
        };
        if (!state.Registered) return state;

        state.Connected = playerManager.IsConnected(player);
        state.PeerBound = playerManager.TryGetPeer(controllerId, out var peer);
        if (!state.PeerBound) return state;

        state.PostJoinTailApplied = connections.HasCompletedCampaignSynchronization(peer);
        state.Loading = connections.LoadingPeers.Any(connection => connection.Peer == peer);
        return state;
    }

    private static string CampaignReadinessFailure(string error) =>
        "LIVE_TEST_JSON={\"success\":false,\"error\":\"" + error + "\"}";

    private static string CampaignReadinessJson(CampaignReadinessState state) =>
        "{\"registered\":" + JsonBoolean(state.Registered) +
        ",\"connected\":" + JsonBoolean(state.Connected) +
        ",\"peerBound\":" + JsonBoolean(state.PeerBound) +
        ",\"postJoinTailApplied\":" + JsonBoolean(state.PostJoinTailApplied) +
        ",\"loading\":" + JsonBoolean(state.Loading) + "}";

    private static string JsonBoolean(bool value) => value ? "true" : "false";

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

    private sealed class CampaignReadinessState
    {
        public bool Registered { get; set; }
        public bool Connected { get; set; }
        public bool PeerBound { get; set; }
        public bool PostJoinTailApplied { get; set; }
        public bool Loading { get; set; }

        public bool Ready =>
            Registered &&
            Connected &&
            PeerBound &&
            PostJoinTailApplied &&
            !Loading;
    }
}
#endif
