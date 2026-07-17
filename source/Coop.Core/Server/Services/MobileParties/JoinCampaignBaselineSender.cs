using Common.Network;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Time.Interfaces;
using LiteNetLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace Coop.Core.Server.Services.MobileParties;

public interface IJoinCampaignBaselineSender
{
    void Send(NetPeer peer);
}

internal sealed class JoinCampaignBaselineSender : IJoinCampaignBaselineSender
{
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapTimeTrackerInterface mapTimeTrackerInterface;

    public JoinCampaignBaselineSender(
        INetwork network,
        IObjectManager objectManager,
        IMapTimeTrackerInterface mapTimeTrackerInterface)
    {
        this.network = network;
        this.objectManager = objectManager;
        this.mapTimeTrackerInterface = mapTimeTrackerInterface;
    }

    public void Send(NetPeer peer)
    {
        var parties = Campaign.Current?.CampaignObjectManager?.MobileParties;
        if (parties == null || !mapTimeTrackerInterface.TryGetCurrentTicks(out long serverTicks))
        {
            throw new InvalidOperationException("Cannot capture a join baseline without a loaded campaign");
        }

        var positions = new List<MobilePartyPositionData>(parties.Count);
        foreach (MobileParty party in parties)
        {
            if (!objectManager.TryGetIdWithLogging(party, out string partyId))
                continue;

            positions.Add(new MobilePartyPositionData(
                Compact(partyId, typeof(MobileParty)),
                party.Position));
        }

        network.SendImmediate(
            peer,
            new NetworkJoinCampaignBaseline(serverTicks, positions.ToArray()));
    }
}
