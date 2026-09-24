using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Players.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

[ProtoContract]
public readonly struct NetworkNewPlayerHeroCreated : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;
    [ProtoMember(2)]
    public readonly Player Player;
    [ProtoMember(3)]
    public readonly byte[] HeroData;
    [ProtoMember(4)]
    public readonly PlayerRegistrationHandles RegistrationHandles;

    public NetworkNewPlayerHeroCreated(
        string controllerId,
        Player player,
        byte[] heroData,
        PlayerRegistrationHandles registrationHandles = default)
    {
        ControllerId = controllerId;
        Player = player;
        HeroData = heroData;
        RegistrationHandles = registrationHandles;
    }
}
