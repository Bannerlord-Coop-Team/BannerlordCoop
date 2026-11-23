using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.UI;
using GameInterface.Services.UI.Messages;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.UI;

public class PlayerKillFeedColorSyncTests : IDisposable
{
    private const string PlayerOne = "PlayerOne";
    private const string PlayerTwo = "PlayerTwo";

    private readonly E2ETestEnvironment testEnvironment;

    private EnvironmentInstance Server => testEnvironment.Server;
    private EnvironmentInstance[] Clients => testEnvironment.Clients.ToArray();

    public PlayerKillFeedColorSyncTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);

        var clients = Clients;
        RegisterPlayer(clients[0], PlayerOne);
        RegisterPlayer(clients[1], PlayerTwo);
    }

    public void Dispose()
    {
        testEnvironment.Dispose();
    }

    [Fact]
    public void ClientColorRequests_ServerStoresAndAllClientsUseColors()
    {
        var color = new PlayerKillFeedColor(12, 34, 56);
        var clients = Clients;

        SendColor(clients[0], color);

        AssertStored(Server, PlayerOne, color);
        foreach (var client in clients)
        {
            AssertStored(client, PlayerOne, color);
        }

        // The same color is allowed for a different player, and the later requester gets the existing snapshot.
        Server.NetworkSentMessages.Clear();
        SendColor(clients[1], color);

        var updates = Server.NetworkSentMessages.GetMessages<NetworkUpdateKillFeedColor>().ToArray();
        Assert.Contains(updates, update => Matches(update, PlayerOne, color));
        Assert.Contains(updates, update => Matches(update, PlayerTwo, color));

        AssertStored(Server, PlayerTwo, color);
        foreach (var client in clients)
        {
            AssertStored(client, PlayerOne, color);
            AssertStored(client, PlayerTwo, color);
        }
    }

    [Fact]
    public void InvalidColorRequest_DoesNotOverwriteOrBroadcast()
    {
        var validColor = new PlayerKillFeedColor(7, 8, 9);
        var client = Clients[0];

        SendColor(client, validColor);
        Server.NetworkSentMessages.Clear();

        client.Call(() =>
            client.Resolve<INetwork>().SendAll(new NetworkRequestKillFeedColor(-1, 300, 10)));

        AssertStored(Server, PlayerOne, validColor);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkUpdateKillFeedColor>());
    }

    private void RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        client.Call(() =>
        {
            client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
            client.Resolve<IPlayerManager>().AddPlayer(new Player(
                controllerId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        });

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            playerManager.AddPlayer(new Player(
                controllerId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
            playerManager.SetPeer(controllerId, client.NetPeer);
        });
    }

    private static void SendColor(EnvironmentInstance client, PlayerKillFeedColor color)
    {
        client.Call(() =>
            client.Resolve<INetwork>().SendAll(new NetworkRequestKillFeedColor(color.Red, color.Green, color.Blue)));
    }

    private static void AssertStored(EnvironmentInstance instance, string controllerId, PlayerKillFeedColor expected)
    {
        instance.Call(() =>
        {
            Assert.True(instance.Resolve<IPlayerKillFeedColorService>().TryGetColor(controllerId, out var actual));
            Assert.Equal(expected, actual);
        });
    }

    private static bool Matches(NetworkUpdateKillFeedColor update, string controllerId, PlayerKillFeedColor color)
    {
        return update.ControllerId == controllerId &&
            update.Red == color.Red &&
            update.Green == color.Green &&
            update.Blue == color.Blue;
    }
}
