using Common.Messaging;
using GameInterface.Services.Players.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Players.Messages;
/// <summary>
/// Once a player is registered on the server. The server will send this 
/// to the client to notify them of a new player.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkRegisterPlayer : ICommand
{
    [ProtoMember(1)]
    public Player Player { get; set; }

    public NetworkRegisterPlayer(Player player)
    {
        this.Player = player;
    }
}
