using GameInterface.Services.Players;
using LiteNetLib;

namespace E2E.Tests.Environment.Extensions;

/// <summary>Test setup that binds a peer by controller id through <see cref="IPlayerManager.TrySetPeer"/>.</summary>
public static class PlayerManagerExtensions
{
    public static void SetPeer(this IPlayerManager players, string controllerId, NetPeer peer)
    {
        Assert.True(players.TryGetPlayer(controllerId, out var player), $"Controller {controllerId} is not registered");
        Assert.True(players.TrySetPeer(player, peer), $"Binding a peer to controller {controllerId} was refused");
    }
}
