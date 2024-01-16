using Common.Messaging;
using GameInterface.Services.Players.Data;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Players.Messages;

[ProtoContract]
internal class NetworkRegisterPlayer : ICommand
{
    [ProtoMember(1)]
    public Player Player { get; set; }

    public NetworkRegisterPlayer(Player player)
    {
        this.Player = player;
    }
}
