using Common.Commands;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Commands;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;
using HarmonyLib;
using LiteNetLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Players;

/// <summary>
/// Verifies the coop.delete_player flow: the command forwards a
/// <see cref="NetworkRequestDeletePlayer"/> to the server, which resolves the player from the
/// requesting connection, deletes its registration everywhere (<see cref="NetworkPlayerRemoved"/>
/// first, so the other clients lift the player-party destroy protection), kicks the requester,
/// kills the hero, and destroys the party. Requests that cannot be applied are answered with
/// <see cref="NetworkDeletePlayerDenied"/> and nothing changes.
/// </summary>
public class DeletePlayerCommandTests : IDisposable
{
    /// <summary>The obituary is a game-content lookup the test harness cannot serve; same
    /// disable as the companion removal tests.</summary>
    private static readonly System.Reflection.MethodBase CreateObituaryMethod =
        AccessTools.Method(typeof(KillCharacterAction), "CreateObituary");

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();

    public DeletePlayerCommandTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void DeletePlayer_OnServer_IsRejected()
    {
        string output = null;
        Server.Call(() =>
        {
            output = new DeletePlayerCommand.PlayerDeleteCoopCommand()
                .ProcessCommand(new CoopCommandArgsFactory().FromValues(Array.Empty<string>()))
                .Output;
        });

        Assert.Equal("Command can only be run on a client.", output);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRequestDeletePlayer>());
    }

    [Fact]
    public void DeletePlayer_OnClient_SendsRequestToServer()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        // Point the client's main hero at a registered hero (the environment's default main hero
        // is client-local and unregistered).
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));

            using (new AllowedThread())
            {
                character.HeroObject = hero;
                Game.Current.PlayerTroop = character;
            }
        });

        string output = null;
        Client.Call(() =>
        {
            output = new DeletePlayerCommand.PlayerDeleteCoopCommand()
                .ProcessCommand(new CoopCommandArgsFactory().FromValues(Array.Empty<string>()))
                .Output;
        });

        Assert.Contains("Delete request sent", output);
        Assert.Single(Client.InternalMessages.GetMessages<PlayerDeleteRequested>());

        var request = Assert.Single(Client.NetworkSentMessages.GetMessages<NetworkRequestDeletePlayer>());
        Assert.Equal(heroId, request.HeroId);
    }

    [Fact]
    public void ServerDeleteRequest_DeletesPlayerKillsHeroDestroysPartyAndDisconnects()
    {
        var fixture = SetupRegisteredPlayer();

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new NetworkRequestDeletePlayer(fixture.HeroId));
        }, new[] { CreateObituaryMethod });
        TestEnvironment.FlushCoalescer();

        // The registration is gone on the server, and the removal broadcast dropped it on the
        // remaining client.
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.False(playerManager.TryGetPlayer(fixture.ControllerId, out _));
            Assert.False(playerManager.TryGetPlayer(Client.NetPeer, out _));
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerRemoved>());
        OtherClient.Call(() =>
        {
            Assert.False(OtherClient.Resolve<IPlayerManager>().TryGetPlayer(fixture.ControllerId, out _));
        });

        // The requester was kicked; being excluded from the removal broadcast, it never
        // deregistered its own player before the teardown.
        Assert.NotEqual(ConnectionState.Connected, Client.NetPeer.ConnectionState);
        Client.Call(() =>
        {
            Assert.True(Client.Resolve<IPlayerManager>().TryGetPlayer(fixture.ControllerId, out _));
        });

        // The hero is dead on the server, and on the remaining client through the removal
        // broadcast's native death transition (the field-sync relay additionally confirms the
        // state in production; its Coop.Core legs are covered by HeroFieldTests).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var hero));
            Assert.False(hero.IsAlive);
        });
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var hero));
            Assert.False(hero.IsAlive);
        });

        // The party was destroyed on the server and the destruction replicated to the remaining
        // client (its protection was lifted by the removal arriving first).
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkApplyDestroyParty>());
        Assert.False(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PartyId, out _));
        Assert.False(OtherClient.ObjectManager.TryGetObject<MobileParty>(fixture.PartyId, out _));
    }

    [Fact]
    public void ServerDeleteRequest_WithDeactivatedParty_StillDestroysIt()
    {
        // Captivity parks the registered player party with IsActive = false; deletion must not
        // leave that party behind.
        var fixture = SetupRegisteredPlayer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PartyId, out var party));
            party.IsActive = false;
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new NetworkRequestDeletePlayer(fixture.HeroId));
        }, new[] { CreateObituaryMethod });
        TestEnvironment.FlushCoalescer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerRemoved>());
        Assert.False(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PartyId, out _));
        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IPlayerManager>().TryGetPlayer(fixture.ControllerId, out _));
        });
    }

    [Fact]
    public void ServerDeleteRequest_WhileBesieging_IsDeniedAndChangesNothing()
    {
        var fixture = SetupRegisteredPlayer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PartyId, out var party));
            party._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new NetworkRequestDeletePlayer(fixture.HeroId));
        });

        var denial = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkDeletePlayerDenied>());
        Assert.Contains("battle or siege", denial.Reason);

        // The requester surfaced the denial and stays connected with everything intact.
        var denied = Assert.Single(Client.InternalMessages.GetMessages<PlayerDeleteDenied>());
        Assert.Contains("battle or siege", denied.Reason);
        Assert.Equal(ConnectionState.Connected, Client.NetPeer.ConnectionState);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(fixture.ControllerId, out _));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var hero));
            Assert.True(hero.IsAlive);
        });
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PartyId, out _));
    }

    [Fact]
    public void ServerDeleteRequest_WithNoRegisteredPlayer_IsDeniedWithoutKick()
    {
        // Registered player, but its peer was never connected — the request arrives from a
        // connection the server cannot resolve to a player (e.g. mid-join).
        var fixture = SetupRegisteredPlayer(connectPeer: false);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new NetworkRequestDeletePlayer(fixture.HeroId));
        });

        var denial = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkDeletePlayerDenied>());
        Assert.Contains("No registered player", denial.Reason);
        Assert.Equal(ConnectionState.Connected, Client.NetPeer.ConnectionState);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerRemoved>());
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(fixture.ControllerId, out _));
        });
    }

    [Fact]
    public void ClientPlayerRemoved_DeregistersPlayerOnThatClient()
    {
        var fixture = SetupRegisteredPlayer(connectPeer: false);

        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.Resolve<IPlayerManager>().TryGetPlayer(fixture.ControllerId, out _));
        });

        OtherClient.SimulateMessage(this, new NetworkPlayerRemoved(fixture.ControllerId, fixture.HeroId));

        OtherClient.Call(() =>
        {
            Assert.False(OtherClient.Resolve<IPlayerManager>().TryGetPlayer(fixture.ControllerId, out _));
        });
    }

    private record PlayerFixture(string ControllerId, string HeroId, string CharacterId, string PartyId, string ClanId);

    /// <summary>
    /// Registers a hero/party pair as a player on the server and on both clients (the requester
    /// registers its own player at join; the other client mirrors how clients learn about remote
    /// players), optionally connecting the first client's peer as that player's connection.
    /// </summary>
    private PlayerFixture SetupRegisteredPlayer(bool connectPeer = true, string controllerId = "DeleteTestPlayer")
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        string characterId = null;
        string clanId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out characterId));
            Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out clanId));

            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player(controllerId, heroId, partyId, clanId, characterId)));
        });

        foreach (var client in new[] { Client, OtherClient })
        {
            client.Call(() =>
            {
                Assert.True(client.Resolve<IPlayerManager>().AddPlayer(
                    new Player(controllerId, heroId, partyId, clanId, characterId)));
            });
        }

        if (connectPeer)
        {
            TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        }

        return new PlayerFixture(controllerId, heroId, characterId, partyId, clanId);
    }
}
