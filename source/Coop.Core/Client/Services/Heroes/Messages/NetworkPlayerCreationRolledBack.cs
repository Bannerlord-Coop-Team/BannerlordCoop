using Common.Messaging;
using GameInterface.Services.Players.Data;
using ProtoBuf;
using System;

namespace Coop.Core.Client.Services.Heroes.Messages;

[ProtoContract]
public readonly struct NetworkPlayerCreationRolledBack : IEvent
{
    [ProtoMember(1)]
    public readonly Player Player;
    [ProtoMember(2)]
    public readonly string[] RegistrationIds;

    public NetworkPlayerCreationRolledBack(Player player, string[] registrationIds)
    {
        Player = player;
        RegistrationIds = registrationIds ?? Array.Empty<string>();
    }
}
