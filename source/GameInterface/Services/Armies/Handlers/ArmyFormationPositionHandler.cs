using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Armies.Handlers;

/// <summary>Converges client-controlled army-leader positions on the authoritative server while members gather.</summary>
internal sealed class ArmyFormationPositionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyFormationPositionHandler>();

    private readonly Dictionary<string, CampaignVec2> lastReportedPositions = new Dictionary<string, CampaignVec2>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IArmyFormationPositionConvergence convergence;

    public ArmyFormationPositionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IArmyFormationPositionConvergence convergence)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.convergence = convergence;

        messageBroker.Subscribe<ArmyLeaderPositionObserved>(HandleObservedPosition);
        messageBroker.Subscribe<NetworkRequestArmyLeaderPositionConvergence>(HandlePositionRequest);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ArmyLeaderPositionObserved>(HandleObservedPosition);
        messageBroker.Unsubscribe<NetworkRequestArmyLeaderPositionConvergence>(HandlePositionRequest);
    }

    private void HandleObservedPosition(MessagePayload<ArmyLeaderPositionObserved> payload)
    {
        if (ModInformation.IsServer) return;

        MobileParty leaderParty = payload.What.LeaderParty;
        if (!TryCreateState(leaderParty, leaderParty?.IsControlledByThisInstance() == true, out var state))
            return;

        if (!convergence.CanReport(state))
        {
            lastReportedPositions.Remove(state.LeaderPartyId);
            return;
        }

        bool hasPreviousPosition = lastReportedPositions.TryGetValue(
            state.LeaderPartyId,
            out CampaignVec2 previousPosition);
        if (!convergence.ShouldReport(
            state,
            hasPreviousPosition,
            previousPosition))
            return;

        lastReportedPositions[state.LeaderPartyId] = state.Position;
        network.SendAll(new NetworkRequestArmyLeaderPositionConvergence(
            state.LeaderPartyId,
            state.Position));
    }

    private void HandlePositionRequest(MessagePayload<NetworkRequestArmyLeaderPositionConvergence> payload)
    {
        if (ModInformation.IsClient || payload.Who is not NetPeer peer) return;

        var request = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!PeerControlsParty(peer, request.LeaderPartyId)) return;
            if (!objectManager.TryGetObjectWithLogging(request.LeaderPartyId, out MobileParty leaderParty))
                return;
            if (!TryCreateState(leaderParty, isControlled: true, out var state)) return;
            if (!convergence.ShouldApply(state, request.Position)) return;

            CampaignVec2 previousPosition = leaderParty.Position;
            // Keep patches live; Army.Tick remains the only code that attaches the converged member.
            leaderParty.Position = request.Position;
            Logger.Debug(
                "Converged player army leader {PartyId} from {PreviousPosition} to {ReportedPosition} while members gather",
                state.LeaderPartyId,
                previousPosition,
                request.Position);
        }, context: nameof(NetworkRequestArmyLeaderPositionConvergence));
    }

    private bool PeerControlsParty(NetPeer peer, string partyId)
    {
        if (playerManager.TryGetPlayer(peer, out var player) &&
            string.Equals(
                Compact(player.MobilePartyId, typeof(MobileParty)),
                Compact(partyId, typeof(MobileParty)),
                StringComparison.Ordinal))
            return true;

        Logger.Warning(
            "Ignoring army leader position convergence from a peer that does not control {PartyId}",
            partyId);
        return false;
    }

    private bool TryCreateState(
        MobileParty leaderParty,
        bool isControlled,
        out ArmyFormationPositionState state)
    {
        state = default;
        if (leaderParty == null || !objectManager.TryGetId(leaderParty, out string leaderPartyId))
            return false;

        leaderPartyId = Compact(leaderPartyId, typeof(MobileParty));
        Army army = leaderParty.Army;
        bool isArmyLeader = army?.LeaderParty == leaderParty;
        bool hasConvergingMember = false;
        bool hasNearbyConvergingMember = false;
        if (isArmyLeader && Campaign.Current?.Models?.EncounterModel != null)
        {
            float attachmentDistanceSquared = leaderParty.IsCurrentlyAtSea
                ? Campaign.Current.Models.EncounterModel.MaximumAllowedNavalDistanceForEncounteringMobilePartyInArmy
                : Campaign.Current.Models.EncounterModel.MaximumAllowedLandDistanceForEncounteringMobilePartyInArmy;
            hasConvergingMember = HasConvergingMember(
                army,
                leaderParty,
                attachmentDistanceSquared,
                out hasNearbyConvergingMember);
        }

        state = new ArmyFormationPositionState(
            leaderPartyId,
            leaderParty.Position,
            leaderParty.IsActive,
            isControlled,
            isArmyLeader,
            leaderParty.AttachedTo != null,
            leaderParty.MapEvent != null,
            leaderParty.CurrentSettlement != null,
            leaderParty.IsCurrentlyAtSea,
            hasConvergingMember,
            hasNearbyConvergingMember);
        return true;
    }

    private static bool HasConvergingMember(
        Army army,
        MobileParty leaderParty,
        float attachmentDistanceSquared,
        out bool hasNearbyConvergingMember)
    {
        hasNearbyConvergingMember = false;
        bool hasConvergingMember = false;
        // Mirror Army.Tick's non-distance gates so ordinary army travel never sends corrections.
        foreach (MobileParty member in army.Parties)
        {
            if (member == leaderParty ||
                member.Army != army ||
                member.AttachedTo != null ||
                member.ShortTermTargetParty != leaderParty ||
                member.MapEvent != null ||
                member.IsCurrentlyAtSea != leaderParty.IsCurrentlyAtSea)
                continue;

            hasConvergingMember = true;
            if ((member.Position - leaderParty.Position).LengthSquared < attachmentDistanceSquared)
                hasNearbyConvergingMember = true;
        }

        return hasConvergingMember;
    }
}
