using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Here transfer event that contains the data to reconstruct that hero
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTransferNewHero : IEvent
{
    [ProtoMember(1)]
    public readonly byte[] PlayerHero;

    public NetworkTransferNewHero(byte[] playerHero)
    {
        PlayerHero = playerHero;
    }
}
