using Common.Network;
using GameInterface.Services.UI.Messages;
using LiteNetLib;

namespace GameInterface.Services.UI.Interfaces;

public interface ITacticalUnitSymbolsConfigInterface : IGameAbstraction
{
    void SendSnapshot(NetPeer peer);
}

internal class TacticalUnitSymbolsConfigInterface : ITacticalUnitSymbolsConfigInterface
{
    private readonly INetwork network;

    public TacticalUnitSymbolsConfigInterface(INetwork network)
    {
        this.network = network;
    }

    public void SendSnapshot(NetPeer peer)
    {
        network.Send(peer, new NetworkTacticalUnitSymbolsVisibilityChanged(
            TacticalUnitSymbolsSettings.HideTacticalUnitSymbols));
    }
}
