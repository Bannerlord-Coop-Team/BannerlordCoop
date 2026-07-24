using Common.Logging;
using Common.Network;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.Time.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

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

    public JoinCampaignBaselineSender(
        INetwork network,
        IMapTimeTrackerInterface mapTimeTrackerInterface,
        IMobilePartyBehaviorSnapshot mobilePartyBehaviorSnapshot)
    {
        this.network = network;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;
        this.mobilePartyBehaviorSnapshot = mobilePartyBehaviorSnapshot;
    }

    public void Send(NetPeer peer)
    {
        var parties = Campaign.Current?.CampaignObjectManager?.MobileParties;
        if (parties == null || !mapTimeTrackerInterface.TryGetCurrentTicks(out long serverTicks))
        {
            throw new InvalidOperationException("Cannot capture a join baseline without a loaded campaign");
        }

        var partyStates = new MobilePartyJoinState[parties.Count];
        bool isComplete = true;
        for (int i = 0; i < parties.Count; i++)
        {
            MobileParty party = parties[i];
            if (!mobilePartyBehaviorSnapshot.TryCreateJoinState(party, out MobilePartyJoinState state))
            {
                Logger.Warning("Could not capture a complete join baseline for party {Party}", party?.StringId);
                isComplete = false;
                break;
            }

            PartyBehaviorUpdateData behavior = state.Behavior;
            behavior.ForcePosition = true;
            state.Behavior = behavior;
            partyStates[i] = state;
        }

        if (isComplete == false) partyStates = Array.Empty<MobilePartyJoinState>();

        network.SendImmediate(
            peer,
            new NetworkJoinCampaignBaseline(serverTicks, partyStates, isComplete));
    }
}
