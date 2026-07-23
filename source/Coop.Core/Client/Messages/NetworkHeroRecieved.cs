using Common.Messaging;
using GameInterface.Services.Players.Data;
using ProtoBuf;

namespace Coop.Core.Client.Messages;

[ProtoContract]
internal readonly struct NetworkHeroRecieved : IEvent
{
    [ProtoMember(1)]
    public readonly Player Player;

    public NetworkHeroRecieved(Player player)
    {
        Player = player;
    }
}
