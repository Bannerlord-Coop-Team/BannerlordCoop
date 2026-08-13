using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.TroopRosters;

public interface IPlayerTroopXpRelevance
{
    bool IsRelevant(MobileParty party, Player player);
    IReadOnlyCollection<NetPeer> GetConnectedPeers(MobileParty party);
}

internal sealed class PlayerTroopXpRelevance : IPlayerTroopXpRelevance
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public PlayerTroopXpRelevance(IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public bool IsRelevant(MobileParty party, Player player)
    {
        if (party == null || player == null ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
        {
            return false;
        }

        if (ReferenceEquals(party, playerParty)) return true;
        if (PlayerManager.TryGetControlledObjectInfo(party, out _)) return false;

        return party.ActualClan != null && ReferenceEquals(party.ActualClan, playerParty.ActualClan);
    }

    public IReadOnlyCollection<NetPeer> GetConnectedPeers(MobileParty party)
    {
        var peers = new HashSet<NetPeer>();
        foreach (var player in playerManager.Players)
        {
            if (IsRelevant(party, player) &&
                playerManager.TryGetPeer(player.ControllerId, out var peer))
            {
                peers.Add(peer);
            }
        }

        return peers;
    }
}
