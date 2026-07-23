using LiteNetLib;

namespace Coop.Core.Client.Services.Heroes.Data;

public class TransferredHeroData
{
    public NetPeer NetPeer { get; private set; }
    public string ControllerId { get; }
    public byte[] HeroData { get; }

    public TransferredHeroData(NetPeer netPeer, string controllerId, byte[] heroData)
    {
        NetPeer = netPeer;
        ControllerId = controllerId;
        HeroData = heroData;
    }
}
