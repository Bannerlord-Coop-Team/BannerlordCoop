using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// Party screen commits that move a companion between the player's party and a clan party, or make a new one.
/// </summary>
public class PartyScreenCommitClanPartyTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.Single();

    public PartyScreenCommitClanPartyTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
    }

    // The manage troops screen of a clan party moves the companion into that party's real roster.
    [Fact]
    public void CompanionMovedToAClanParty_BelongsToThatParty_OnServerAndClient()
    {
        var fixture = CreateFixture();

        Send(PartyScreenCommits.Create(
            fixture.MainHeroId,
            Helpers.PartyScreenHelper.PartyScreenMode.Normal,
            PartyScreenDoneKind.None,
            new[] { MoveCompanion(fixture) },
            leftPartyId: fixture.ClanPartyBaseId));

        foreach (var instance in new[] { Server, Client })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.CompanionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(fixture.ClanPartyId, out var clanParty));
                Assert.Same(clanParty, companion.PartyBelongedTo);
            });
        }
    }

    // Taking the companion back has to leave their old roster before joining the new one, since leaving clears
    // PartyBelongedTo whichever party it points at.
    [Fact]
    public void CompanionTakenBackFromAClanParty_BelongsToThePlayerParty_OnServerAndClient()
    {
        var fixture = CreateFixture();
        Send(PartyScreenCommits.Create(
            fixture.MainHeroId,
            Helpers.PartyScreenHelper.PartyScreenMode.Normal,
            PartyScreenDoneKind.None,
            new[] { MoveCompanion(fixture) },
            leftPartyId: fixture.ClanPartyBaseId));

        Send(PartyScreenCommits.Create(
            fixture.MainHeroId,
            Helpers.PartyScreenHelper.PartyScreenMode.Normal,
            PartyScreenDoneKind.None,
            new[] { MoveCompanion(fixture, PartyScreenLogic.PartyRosterSide.Left) },
            leftPartyId: fixture.ClanPartyBaseId));

        foreach (var instance in new[] { Server, Client })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.CompanionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerPartyId, out var playerParty));
                Assert.Same(playerParty, companion.PartyBelongedTo);
            });
        }
    }

    // The create clan party screen starts with the companion on the left and the server builds the party from
    // what the commit moved there.
    [Fact]
    public void CreateClanPartyCommit_MakesAPartyLedByTheCompanion_WithTheMovedTroops()
    {
        var fixture = CreateFixture();
        uint troopHandle = Server.GetHandle<CharacterObject>(fixture.TroopId);
        uint companionHandle = Server.GetHandle<CharacterObject>(fixture.CompanionCharacterId);

        Send(PartyScreenCommits.Create(
            fixture.MainHeroId,
            Helpers.PartyScreenHelper.PartyScreenMode.TroopsManage,
            PartyScreenDoneKind.CreateClanParty,
            new[] { PartyScreenCommits.Transfer(PartyScreenLogic.PartyRosterSide.Right, PartyScreenLogic.TroopType.Member, troopHandle, 2) },
            leftMemberSeed: new TroopRosterData(new[] { new TroopRosterElementData(companionHandle, 1, 0, 0) })));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(fixture.TroopId, out var troop));
            var newParty = companion.PartyBelongedTo;
            Assert.NotNull(newParty);
            Assert.NotSame(playerParty, newParty);
            Assert.Same(companion, newParty.LeaderHero);
            Assert.Equal(2, newParty.MemberRoster.GetTroopCount(troop));
            Assert.Equal(1, playerParty.MemberRoster.GetTroopCount(troop));
            Assert.False(playerParty.MemberRoster.Contains(companion.CharacterObject));
        });
    }

    private PartyScreenCommandData MoveCompanion(ClanPartyFixture fixture, PartyScreenLogic.PartyRosterSide from = PartyScreenLogic.PartyRosterSide.Right)
        => PartyScreenCommits.Transfer(
            from,
            PartyScreenLogic.TroopType.Member,
            Server.GetHandle<CharacterObject>(fixture.CompanionCharacterId),
            1);

    private ClanPartyFixture CreateFixture()
    {
        ClanPartyFixture fixture = default;
        Server.Call(() =>
        {
            var playerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var clanParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var companion = GameObjectCreator.CreateInitializedObject<Hero>();
            var troop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            var mainHero = playerParty.LeaderHero;
            if (mainHero.PartyBelongedTo == null) mainHero.PartyBelongedTo = playerParty;
            companion.Clan = mainHero.Clan;
            clanParty._actualClan = mainHero.Clan;
            playerParty.MemberRoster.AddToCounts(companion.CharacterObject, 1);
            playerParty.MemberRoster.AddToCounts(troop, 3);

            Assert.True(Server.ObjectManager.TryGetId(mainHero, out var mainHeroId));
            Assert.True(Server.ObjectManager.TryGetId(playerParty, out var playerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(clanParty, out var clanPartyId));
            Assert.True(Server.ObjectManager.TryGetId(clanParty.Party, out var clanPartyBaseId));
            Assert.True(Server.ObjectManager.TryGetId(companion, out var companionId));
            Assert.True(Server.ObjectManager.TryGetId(companion.CharacterObject, out var companionCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("ClanPartyControllerId", mainHeroId, playerPartyId, string.Empty, string.Empty)));
            playerManager.SetPeer("ClanPartyControllerId", Client.NetPeer);
            fixture = new ClanPartyFixture(
                mainHeroId, playerPartyId, clanPartyId, clanPartyBaseId, companionId, companionCharacterId, troopId);
        });
        TestEnvironment.FlushCoalescer();
        return fixture;
    }

    private void Send(GameInterface.Services.Party.Messages.NetworkCommitPartyScreen message)
    {
        Client.Call(() => Client.Resolve<INetwork>().SendAll(message));
        TestEnvironment.FlushCoalescer();
    }

    public void Dispose() => TestEnvironment.Dispose();

    private readonly record struct ClanPartyFixture(
        string MainHeroId,
        string PlayerPartyId,
        string ClanPartyId,
        string ClanPartyBaseId,
        string CompanionId,
        string CompanionCharacterId,
        string TroopId);
}
