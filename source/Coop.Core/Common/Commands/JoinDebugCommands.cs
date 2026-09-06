#if DEBUG
using System;
using Common.Commands;
using Common;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Server.Connections;
using GameInterface;
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

    public sealed class ArmInactivePartyDeficitCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.connection";

        public string Name => "arm_inactive_party_deficit";

        public string Description => "Arms the next client join baseline to omit an inactive party.";

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
}
#endif
