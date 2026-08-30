using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.BugReporting;
using GameInterface.Services.BugReporting.Messages;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

/// <summary>Skips unstuck bug-report tests while the feature is disabled.</summary>
public sealed class UnstuckCommandReportingFactAttribute : FactAttribute
{
    public UnstuckCommandReportingFactAttribute()
    {
        if (!BugReportConfig.UnstuckCommandReportsEnabled)
            Skip = "Automatic unstuck bug reports are disabled.";
    }
}

/// <summary>
/// Verifies the dedicated unstuck flow: coop.debug.mobileparty.unstuck forwards a
/// <see cref="NetworkRequestPlayerUnstuck"/> to the server, the server force-applies each exit
/// independently and replies with <see cref="NetworkPlayerUnstuckResult"/>, and the requesting
/// client runs local cleanup and publishes <see cref="PlayerUnstuckCompleted"/>.
/// </summary>
public class UnstuckCommandTests : MapEventTestBase
{
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance SecondClient => TestEnvironment.Clients.Skip(1).First();

    public UnstuckCommandTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Unstuck_OnServer_IsRejected()
    {
        string output = null;
        Server.Call(() =>
        {
            output = UnstuckCommand.Unstuck(new List<string>());
        });

        Assert.Equal("Command can only be run on a client.", output);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRequestPlayerUnstuck>());
    }

    [Fact]
    public void UnstuckCommand_OnClient_SendsRequestToServer()
    {
        var player = SetupRegisteredMainHeroAndParty();

        string output = null;
        Client.Call(() =>
        {
            output = UnstuckCommand.Unstuck(new List<string>());
        });

        Assert.Contains("Unstuck request sent", output);
        Assert.Single(Client.InternalMessages.GetMessages<PlayerUnstuckRequested>());

        var request = Assert.Single(Client.NetworkSentMessages.GetMessages<NetworkRequestPlayerUnstuck>());
        Assert.Equal(player.PartyId, request.PartyId);
        Assert.Equal(player.HeroId, request.HeroId);
    }

    [UnstuckCommandReportingFact]
    public void ServerUnstuckRequest_RequestsDiagnosticLogsFromEveryConnectedClient()
    {
        var requester = SetupRegisteredMainHeroAndParty();
        var secondHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var secondCharacterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var secondPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                "unstuck-report-requester",
                requester.HeroId,
                requester.PartyId,
                null,
                requester.CharacterId)));
            Assert.True(playerManager.AddPlayer(new Player(
                "unstuck-report-second",
                secondHeroId,
                secondPartyId,
                null,
                secondCharacterId)));
            playerManager.SetPeer("unstuck-report-requester", Client.NetPeer);
            playerManager.SetPeer("unstuck-report-second", SecondClient.NetPeer);
        });

        Server.SimulateMessage(
            Client.NetPeer,
            new NetworkRequestPlayerUnstuck(requester.PartyId, requester.HeroId));

        Assert.Collection(
            Server.NetworkSentMessages.GetMessages<NetworkRequestBugReportLogs>(),
            _ => { },
            _ => { });
    }

    [Fact]
    public void PlayerBugReport_RequestsDiagnosticLogsFromEveryConnectedClient()
    {
        var requester = SetupRegisteredMainHeroAndParty();
        var secondHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var secondCharacterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var secondPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                "bug-report-requester",
                requester.HeroId,
                requester.PartyId,
                null,
                requester.CharacterId)));
            Assert.True(playerManager.AddPlayer(new Player(
                "bug-report-second",
                secondHeroId,
                secondPartyId,
                null,
                secondCharacterId)));
            playerManager.SetPeer("bug-report-requester", Client.NetPeer);
            playerManager.SetPeer("bug-report-second", SecondClient.NetPeer);
        });

        SecondClient.Call(() => ((IDisposable)SecondClient.Resolve<IBugReportService>()).Dispose());

        Server.SimulateMessage(
            Client.NetPeer,
            new NetworkRequestBugReport(
                "Party is stuck",
                "Leaving Danustica keeps reopening the town menu."));

        Assert.Collection(
            Server.NetworkSentMessages.GetMessages<NetworkRequestBugReportLogs>(),
            _ => { },
            _ => { });
    }

    [Fact]
    public void RepeatedBugReportWhileCollectionIsActive_DoesNotRequestLogsAgain()
    {
        var requester = SetupRegisteredMainHeroAndParty();
        var secondHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var secondCharacterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var secondPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(
                "repeated-report-requester",
                requester.HeroId,
                requester.PartyId,
                null,
                requester.CharacterId)));
            Assert.True(playerManager.AddPlayer(new Player(
                "repeated-report-second",
                secondHeroId,
                secondPartyId,
                null,
                secondCharacterId)));
            playerManager.SetPeer("repeated-report-requester", Client.NetPeer);
            playerManager.SetPeer("repeated-report-second", SecondClient.NetPeer);
        });

        Client.Call(() => ((IDisposable)Client.Resolve<IBugReportService>()).Dispose());
        SecondClient.Call(() => ((IDisposable)SecondClient.Resolve<IBugReportService>()).Dispose());

        var request = new NetworkRequestBugReport(
            "Party is stuck",
            "Leaving Danustica keeps reopening the town menu.");
        Server.SimulateMessage(Client.NetPeer, request);
        Server.SimulateMessage(Client.NetPeer, request);

        Assert.Equal(
            2,
            Server.NetworkSentMessages.GetMessages<NetworkRequestBugReportLogs>().Count());
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBugReportResult>());
        Assert.Contains("already in progress", result.Message);
    }

    [Fact]
    public void ServerUnstuckRequest_ClearsArmyAndSettlement_AndReportsResult()
    {
        var player = SetupRegisteredMainHeroAndParty();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var leaderPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderPartyId, out var leaderParty));

            // Created outside AllowedThread so the army registers with the object manager.
            var army = new Army(kingdom, leaderParty, Army.ArmyTypes.Patrolling);
            Assert.True(Server.ObjectManager.TryGetId(army, out _));

            using (new AllowedThread())
            {
                // The builder settlement has no component; the native leave calls
                // SettlementComponent.OnPartyLeft, so wire the (no-op) town component.
                settlement.SettlementComponent = town;

                // Keep the leader in the army so removing the player does not cascade into a disband.
                if (!army._parties.Contains(leaderParty)) army._parties.Add(leaderParty);
                army._parties.Add(party);
                party._army = army;

                try
                {
                    party.CurrentSettlement = settlement;
                }
                catch (NullReferenceException)
                {
                    party.SetCurrentSettlementDirectly(settlement);
                }
            }
        });

        Server.SimulateMessage(this, new NetworkRequestPlayerUnstuck(player.PartyId, player.HeroId));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));

            Assert.Null(party.Army);
            Assert.Null(party.CurrentSettlement);
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Equal(player.PartyId, result.PartyId);
        Assert.NotNull(result.Actions);
        Assert.Contains(result.Actions, action => action.Contains("army"));
        Assert.Contains(result.Actions, action => action.Contains("settlement"));
    }

    [Fact]
    public void ServerUnstuckRequest_WithCaptiveHero_ReportsCaptivityStep()
    {
        var player = SetupRegisteredMainHeroAndParty();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));

            using (new AllowedThread())
            {
                hero._heroState = Hero.CharacterStates.Prisoner;
            }
        });

        Server.SimulateMessage(this, new NetworkRequestPlayerUnstuck(player.PartyId, player.HeroId));

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Equal(player.PartyId, result.PartyId);
        Assert.NotNull(result.Actions);
        Assert.Contains(result.Actions, action => action.Contains("captivity release"));
    }

    [Fact]
    public void ServerUnstuckRequest_WithMapEvent_RemovesPartyAndBroadcastsLeave()
    {
        var player = SetupRegisteredMainHeroAndParty();
        var mapEventContext = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));

            party.Party.MapEventSide = mapEvent.AttackerSide;

            Assert.Same(mapEvent, party.MapEvent);
        }, MapEventDisabledMethods);

        Server.SimulateMessage(this, new NetworkRequestPlayerUnstuck(player.PartyId, player.HeroId));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.Null(party.MapEvent);
        });

        var leave = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.False(leave.LeaveSiege);

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Contains(result.Actions, action => action.Contains("map event"));
    }

    [Fact]
    public void ServerUnstuckRequest_WithNothingStuck_ReportsClean()
    {
        var player = SetupRegisteredMainHeroAndParty();

        Server.SimulateMessage(this, new NetworkRequestPlayerUnstuck(player.PartyId, player.HeroId));

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>());
        Assert.Equal(player.PartyId, result.PartyId);
        Assert.NotNull(result.Actions);
        Assert.Contains(result.Actions, action => action.Contains("No server-side stuck state"));
    }

    [Fact]
    public void ClientUnstuckResult_ForOwnParty_RunsLocalCleanupAndPublishesCompleted()
    {
        var player = SetupRegisteredMainHeroAndParty();

        Client.SimulateMessage(this, new NetworkPlayerUnstuckResult(player.PartyId, new[] { "server action" }));

        var completed = Assert.Single(Client.InternalMessages.GetMessages<PlayerUnstuckCompleted>());
        Assert.Equal(player.PartyId, completed.PartyId);
        Assert.Contains("server action", completed.Actions);
    }

    [Fact]
    public void ClientUnstuckResult_ForOtherParty_IsIgnored()
    {
        SetupRegisteredMainHeroAndParty();
        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Client.SimulateMessage(this, new NetworkPlayerUnstuckResult(otherPartyId, new[] { "server action" }));

        Assert.Empty(Client.InternalMessages.GetMessages<PlayerUnstuckCompleted>());
    }

    private record PlayerIds(string HeroId, string CharacterId, string PartyId);

    /// <summary>
    /// Points the client's main hero and main party at server-registered objects so the unstuck
    /// flow can resolve coop ids (the environment's default main hero is client-local and
    /// unregistered).
    /// </summary>
    private PlayerIds SetupRegisteredMainHeroAndParty()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                character.HeroObject = hero;
                hero.PartyBelongedTo = party;
                Game.Current.PlayerTroop = character;
                Campaign.Current.MainParty = party;
            }
        });

        return new PlayerIds(heroId, characterId, partyId);
    }
}
