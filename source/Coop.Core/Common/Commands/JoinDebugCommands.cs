#if DEBUG
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

internal static class JoinDebugCommands
{
    private static int forceNextInactivePartyDeficit;
    private static string desiredInactivePartyId;
    private static string lastForcedPartyId = "none";
    private static MobileParty stagedInactiveParty;
    private static bool stagedInactivePartyWasActive;

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
            return $"stagedParty={stagedInactiveParty.StringId} active={stagedInactiveParty.IsActive}";
        }

        stagedInactiveParty = Campaign.Current?.CampaignObjectManager?.MobileParties
            .FirstOrDefault(party =>
                party?.IsActive == true &&
                !party.IsPlayerParty() &&
                party.MapEvent == null &&
                party.CurrentSettlement == null);
        if (stagedInactiveParty == null)
        {
            return "No active non-player field party was available for the fixture.";
        }

        stagedInactivePartyWasActive = stagedInactiveParty.IsActive;
        stagedInactiveParty.IsActive = false;
        return $"stagedParty={stagedInactiveParty.StringId} active={stagedInactiveParty.IsActive}";
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

    private static string GetPartyCounts()
    {
        var parties = Campaign.Current?.CampaignObjectManager?.MobileParties;
        if (parties == null) return "partyTotal=-1 activeParties=-1 inactiveParties=-1";

        int active = parties.Count(party => party?.IsActive == true);
        return $"partyTotal={parties.Count} activeParties={active} " +
               $"inactiveParties={parties.Count - active}";
    }
}
#endif
