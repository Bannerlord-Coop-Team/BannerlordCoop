using Common.Network;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace Coop.Core.Server.Services.MobileParties;

public interface IJoinMobilePartyPositionSnapshotSender
{
    void Send(NetPeer peer);
}

internal sealed class JoinMobilePartyPositionSnapshotSender : IJoinMobilePartyPositionSnapshotSender
{
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public JoinMobilePartyPositionSnapshotSender(INetwork network, IObjectManager objectManager)
    {
        this.network = network;
        this.objectManager = objectManager;
    }

    public void Send(NetPeer peer)
    {
        var parties = Campaign.Current?.CampaignObjectManager?.MobileParties;
        if (parties == null) return;

        var positions = new List<MobilePartyPositionData>(parties.Count);
        foreach (MobileParty party in parties)
        {
            if (!objectManager.TryGetIdWithLogging(party, out string partyId))
                continue;

            positions.Add(new MobilePartyPositionData(
                Compact(partyId, typeof(MobileParty)),
                party.Position));
        }

        network.SendImmediate(peer, new NetworkJoinMobilePartyPositions(positions.ToArray()));
    }
}
