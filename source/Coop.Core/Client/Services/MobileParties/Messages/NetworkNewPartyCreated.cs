using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// New party added on the server
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkNewPartyCreated : IEvent
{
    [ProtoMember(1)]
    public string PlayerId { get; }

    [ProtoMember(2)]
    public byte[] PlayerHero { get; }

    public NetworkNewPartyCreated(string playerId, byte[] playerHero)
    {
        PlayerId = playerId;
        PlayerHero = playerHero;
    }
}
