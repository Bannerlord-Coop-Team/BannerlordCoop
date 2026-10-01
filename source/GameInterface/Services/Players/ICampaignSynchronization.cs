using LiteNetLib;

namespace GameInterface.Services.Players;

/// <summary>Reads campaign synchronization from the server's current connection state.</summary>
public interface ICampaignSynchronization
{
    bool HasCompletedCampaignSynchronization(NetPeer peer);
}
