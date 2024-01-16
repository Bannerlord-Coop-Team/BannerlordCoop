using Common.Messaging;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.DotNet;
using TaleWorlds.MountAndBlade.Diamond;

namespace GameInterface.Services.Heroes.Messages;

public record RegisterNewPlayerHero : ICommand
{
    public NetPeer SendingPeer { get; }
    public string ControllerId { get; }
    public byte[] Bytes { get; }

    public RegisterNewPlayerHero(NetPeer sendingPeer, string controllerId, byte[] bytes)
    {
        SendingPeer = sendingPeer;
        ControllerId = controllerId;
        Bytes = bytes;
    }
}

public record NewPlayerHeroRegistered : IResponse
{
    public NetPeer SendingPeer { get; }
    public Player Player { get; }

    public NewPlayerHeroRegistered(NetPeer sendingPeer, Player playerData)
    {
        SendingPeer = sendingPeer;

        Player = playerData;
    }
}