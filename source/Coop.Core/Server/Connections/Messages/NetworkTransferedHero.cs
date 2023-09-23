using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Here transfer event that contains the data to reconstruct that hero
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkTransferedHero : IEvent
{
    [ProtoMember(1)]
    public string PlayerId { get; }

    [ProtoMember(2)]
    public byte[] PlayerHero { get; }

    public NetworkTransferedHero(string playerId, byte[] playerHero)
    {
        PlayerId = playerId;
        PlayerHero = playerHero;
    }
}
