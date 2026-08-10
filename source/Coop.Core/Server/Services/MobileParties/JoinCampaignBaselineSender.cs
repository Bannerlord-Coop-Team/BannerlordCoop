using Common.Logging;
using Common.Network;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.Time.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.MobileParties;

public interface IJoinCampaignBaselineSender
{
    void Send(NetPeer peer);
}

internal sealed class JoinCampaignBaselineSender : IJoinCampaignBaselineSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<JoinCampaignBaselineSender>();

    private readonly INetwork network;
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;
    private readonly IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot;
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IPlayerPartyTroopXpBaselineProvider troopXpBaselineProvider;

    public JoinCampaignBaselineSender(
        INetwork network,
        IMapTimeTrackerInterface mapTimeTrackerInterface,
        IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot,
        ITimeControlInterface timeControlInterface,
        IPlayerPartyTroopXpBaselineProvider troopXpBaselineProvider)
    {
        this.network = network;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;
        this.mobilePartyBehaviorSnapshot = mobilePartyBehaviorSnapshot;
        this.timeControlInterface = timeControlInterface;
        this.troopXpBaselineProvider = troopXpBaselineProvider;
    }

    public void Send(NetPeer peer)
    {
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;
        var parties = campaignObjectManager?.MobileParties;
        var settlements = campaignObjectManager?.Settlements;
        if (parties == null ||
            settlements == null ||
            !mapTimeTrackerInterface.TryGetCurrentTicks(out long serverTicks))
        {
            throw new InvalidOperationException("Cannot capture a join baseline without a loaded campaign");
        }

        var liveParties = new HashSet<MobileParty>(parties);
        var liveSettlements = new HashSet<Settlement>(settlements);
        var partyStates = new MobilePartyJoinState[parties.Count];
        TroopRosterXpBaseline[] troopXpBaselines = Array.Empty<TroopRosterXpBaseline>();
        bool isComplete = true;
        for (int i = 0; i < parties.Count; i++)
        {
            MobileParty party = parties[i];
            if (!mobilePartyBehaviorSnapshot.TryCreateJoinState(
                party,
                liveParties,
                liveSettlements,
                out MobilePartyJoinState state,
                out string failure))
            {
                Logger.Warning(
                    "Could not capture a complete join baseline for party {Party}: {Failure}",
                    party?.StringId,
                    failure);
                isComplete = false;
                break;
            }

            PartyBehaviorUpdateData behavior = state.Behavior;
            behavior.ForcePosition = true;
            state.Behavior = behavior;
            partyStates[i] = state;
        }

        if (isComplete && !troopXpBaselineProvider.TryCapture(peer, out troopXpBaselines))
        {
            Logger.Warning("Could not capture the joining player's troop XP baseline");
            isComplete = false;
        }

        if (isComplete == false)
        {
            partyStates = Array.Empty<MobilePartyJoinState>();
            troopXpBaselines = Array.Empty<TroopRosterXpBaseline>();
        }

        network.SendImmediate(
            peer,
            new NetworkJoinCampaignBaseline(
                serverTicks,
                timeControlInterface.GetTimeControl(),
                partyStates,
                isComplete,
                troopXpBaselines));
    }
}
