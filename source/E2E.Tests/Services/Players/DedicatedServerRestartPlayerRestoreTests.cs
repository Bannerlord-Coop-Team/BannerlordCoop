using Common.Messaging;
using Common.Util;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using Coop.Core.Server.Services.Save;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Registry.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Net;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Players;

/// <summary>
/// Regression for #3396: a dedicated-server restart must keep the controller to hero/party/clan mapping.
/// </summary>
public class DedicatedServerRestartPlayerRestoreTests : IDisposable
{
    private const string ControllerId = "PlayerOne";
    private const string UserDirVariable = "BANNERLORD_USER_DIR";

    private readonly ITestOutputHelper output;
    private readonly string saveName = "e2e_ds_restart_" + Guid.NewGuid().ToString("N");
    private readonly string userDir = Path.Combine(Path.GetTempPath(), "coop-e2e-" + Guid.NewGuid().ToString("N"));
    private readonly string? previousUserDir = System.Environment.GetEnvironmentVariable(UserDirVariable);
    private E2ETestEnvironment? liveEnvironment;

    /// <summary>
    /// Points the session json at a throwaway user dir, the way container hosts redirect their save folder.
    /// BANNERLORD_USER_DIR also redirects ModConfig, so a mod-config.json is created in that dir too.
    /// </summary>
    public DedicatedServerRestartPlayerRestoreTests(ITestOutputHelper output)
    {
        this.output = output;
        System.Environment.SetEnvironmentVariable(UserDirVariable, userDir);
    }

    /// <summary>
    /// Shuts down whichever server lifetime is still running and removes the session json it wrote.
    /// </summary>
    public void Dispose()
    {
        try
        {
            liveEnvironment?.Dispose();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(UserDirVariable, previousUserDir);
            DeleteUserDir();
        }
    }

    /// <summary>
    /// Removes the temp user dir without letting a locked file mask an environment disposal failure.
    /// </summary>
    private void DeleteUserDir()
    {
        try
        {
            if (Directory.Exists(userDir))
                Directory.Delete(userDir, recursive: true);
        }
        catch (Exception e)
        {
            output.WriteLine($"Failed to delete temp user dir {userDir}: {e.Message}");
        }
    }

    /// <summary>
    /// Saves a player through the dedicated server's driver, restarts, and checks the returning client keeps its hero.
    /// </summary>
    [Fact]
    public void DedicatedServerRestart_ReturningPlayerResolvesToSavedHero()
    {
        var savedIds = RunFirstServerLifetime();

        var environment = StartServer();
        var server = environment.Server;

        RegisterRecreatedPlayerGraph(server, savedIds);
        RestoreSavedPlayers(server);

        server.Call(() =>
        {
            Assert.True(server.Resolve<IPlayerManager>().TryGetPlayer(ControllerId, out var restored),
                "The restarted server did not restore the saved player registration.");
            Assert.Equal(savedIds.HeroId, restored.HeroId);
            Assert.Equal(savedIds.MobilePartyId, restored.MobilePartyId);
        });

        var client = environment.Clients.First();
        ConnectReturningClient(server, client);

        var validated = Assert.Single(server.NetworkSentImmediateMessages.GetMessages<NetworkClientValidated>());
        Assert.True(validated.HeroExists, "The returning player was sent to character creation.");
        Assert.Equal(savedIds.HeroId, validated.Player.HeroId);

        var connection = server.Resolve<ConnectionCollection>().ConnectionStates[client.NetPeer];
        Assert.IsType<ResolveCharacterState>(connection.State);
        Assert.True(server.Resolve<IPlayerManager>().TryGetPeer(ControllerId, out var peer));
        Assert.Same(client.NetPeer, peer);
    }

    /// <summary>
    /// Registers a player, saves through the dedicated server's driver, checks the sidecar, then shuts down.
    /// </summary>
    private Player RunFirstServerLifetime()
    {
        var environment = StartServer();
        var server = environment.Server;
        var player = CreateRegisteredPlayer(server);

        SaveLikeDedicatedServer(server);

        var saveManager = server.Resolve<ICoopSaveManager>();
        var sessionPath = saveManager.DefaultPath + saveName + saveManager.FileType;
        Assert.True(File.Exists(sessionPath), $"Saving with AsyncFileSaveDriver did not write the co-op session json {sessionPath}");
        Assert.Contains(saveManager.LoadCoopSession(saveName).Players, saved => saved.ControllerId == ControllerId);

        environment.Dispose();
        liveEnvironment = null;
        return player;
    }

    /// <summary>
    /// Builds a fresh server lifetime and tracks it so a failing assertion still tears it down.
    /// </summary>
    private E2ETestEnvironment StartServer()
    {
        liveEnvironment = new E2ETestEnvironment(output, numClients: 1);
        return liveEnvironment;
    }

    /// <summary>
    /// Creates a lord party with its owner hero, clan and character, and registers it as the player's.
    /// </summary>
    private static Player CreateRegisteredPlayer(EnvironmentInstance server)
    {
        Player? player = null;
        server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var hero = party.LordPartyComponent.Owner;

            Assert.True(server.ObjectManager.TryGetId(hero, out var heroId));
            Assert.True(server.ObjectManager.TryGetId(party, out var partyId));
            Assert.True(server.ObjectManager.TryGetId(hero.Clan, out var clanId));
            Assert.True(server.ObjectManager.TryGetId(hero.CharacterObject, out var characterObjectId));

            player = new Player(ControllerId, heroId, partyId, clanId, characterObjectId);
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(player));
        });

        Assert.NotNull(player);
        return player!;
    }

    /// <summary>
    /// Calls the patched Game.Save with the dedicated server's AsyncFileSaveDriver, skipping native serialization.
    /// </summary>
    private void SaveLikeDedicatedServer(EnvironmentInstance server)
    {
        var disabledMethods = new MethodBase[] { AccessTools.Method(typeof(Game), nameof(Game.SaveAux)) };

        server.Call(() =>
        {
            Game.Current.Save(new MetaData(), saveName, new AsyncFileSaveDriver(), _ => { });
        }, disabledMethods);
    }

    /// <summary>
    /// Stands in for the save load: rebuilds the player graph and binds it to the ids the session json holds.
    /// </summary>
    private static void RegisterRecreatedPlayerGraph(EnvironmentInstance server, Player savedIds)
    {
        server.Call(() =>
        {
            MobileParty party;
            using (new AllowedThread())
            {
                party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            }

            var hero = party.LordPartyComponent.Owner;
            Assert.True(server.ObjectManager.AddExisting(savedIds.HeroId, hero));
            Assert.True(server.ObjectManager.AddExisting(savedIds.MobilePartyId, party));
            Assert.True(server.ObjectManager.AddExisting(savedIds.ClanId, hero.Clan));
            Assert.True(server.ObjectManager.AddExisting(savedIds.CharacterObjectId, hero.CharacterObject));
        });
    }

    /// <summary>
    /// Publishes the load-completion messages the load patch and registry manager raise after a save loads.
    /// </summary>
    private void RestoreSavedPlayers(EnvironmentInstance server)
    {
        server.Call(() =>
        {
            var messageBroker = server.Resolve<IMessageBroker>();
            messageBroker.Publish(this, new GameLoaded(saveName));
            messageBroker.Publish(this, new AllGameObjectsRegistered());
        });
    }

    /// <summary>
    /// Runs the returning client's connect and validate handshake against the restarted server.
    /// </summary>
    private void ConnectReturningClient(EnvironmentInstance server, EnvironmentInstance client)
    {
        var endPoint = (IPEndPoint)client.NetPeer;
        if (endPoint.Address == null)
        {
            endPoint.Address = IPAddress.Loopback;
            endPoint.Port = 4200;
        }

        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        server.NetworkSentImmediateMessages.Clear();

        // The join save transfer after resolution needs a real SaveHandler, which the harness campaign lacks.
        var disabledMethods = new MethodBase[]
        {
            AccessTools.Method(typeof(ResolveCharacterState), nameof(ResolveCharacterState.TransferSave)),
        };

        server.Call(() =>
        {
            server.SimulateMessage(this, new PlayerConnected(client.NetPeer));
            server.SimulateMessage(client.NetPeer, new NetworkClientValidate(ControllerId));
        }, disabledMethods);
    }
}
