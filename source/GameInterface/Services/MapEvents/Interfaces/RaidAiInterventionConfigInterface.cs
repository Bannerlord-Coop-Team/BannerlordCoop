using Common.Network;
using GameInterface.Services.MapEvents.Messages;
using LiteNetLib;

namespace GameInterface.Services.MapEvents.Interfaces;

public interface IRaidAiInterventionConfigInterface : IGameAbstraction
{
    void SendSnapshot(NetPeer peer);
}

internal class RaidAiInterventionConfigInterface : IRaidAiInterventionConfigInterface
{
    private readonly INetwork network;

    public RaidAiInterventionConfigInterface(INetwork network)
    {
        this.network = network;
    }

    public void SendSnapshot(NetPeer peer)
    {
        network.Send(peer, new NetworkRaidAiInterventionConfigChanged(MapEventConfig.AllowRaidAiIntervention));
    }
}