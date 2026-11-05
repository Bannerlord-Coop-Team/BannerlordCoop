using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Barbers.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Roles;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

/// <summary>The server applies party and hero management requests only from a player allowed to manage them.</summary>
public class PartyHeroRequestSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => environment.Clients.First();
    private EnvironmentInstance Outsider => environment.Clients.Last();

    private string ownerHeroId;
    private string clanId;
    private string clanPartyId;
    private string clanPartyLeaderId;

    public PartyHeroRequestSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        SetUp();
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartyRole_IsSetOnlyByAPlayerWhoMayManageTheParty(bool fromOwner)
    {
        Send(fromOwner, new NetworkSetPartyScout(clanPartyLeaderId, clanPartyId));

        Server.Call(() => Assert.Equal(fromOwner,
            Get<MobileParty>(clanPartyId).Scout == Get<Hero>(clanPartyLeaderId)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClanName_IsChangedOnlyByTheClanLeader(bool fromOwner)
    {
        var name = new TextObject("Renamed Test Clan");

        Send(fromOwner, new NetworkChangeClanName(clanId, name, name));

        Server.Call(() => Assert.Equal(fromOwner, Get<Clan>(clanId).Name.ToString() == "Renamed Test Clan"));
    }

    [Fact]
    public void BarberChangeOfAnotherPlayersHero_IsRefused()
    {
        string characterId = null;
        float weight = 0;
        BodyProperties properties = default;
        Server.Call(() =>
        {
            var owner = Get<Hero>(ownerHeroId);
            Assert.True(Server.ObjectManager.TryGetId(owner.CharacterObject, out characterId));
            weight = owner.Weight;
            properties = new BodyProperties(new DynamicBodyProperties(30f, weight > 0.5f ? 0.1f : 0.9f, 0.5f), owner.StaticBodyProperties);
        });

        Send(fromOwner: false, new NetworkSaveCurrentCharacter(characterId, properties, 0, false));

        Server.Call(() => Assert.Equal(weight, Get<Hero>(ownerHeroId).Weight));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClanPartyObjective_IsSetOnlyByTheClanLeader(bool fromOwner)
    {
        Server.Call(() => Assert.NotEqual(MobileParty.PartyObjective.Aggressive, Get<MobileParty>(clanPartyId).Objective));

        Send(fromOwner, new UpdatePartyBehaviorOnSelection(clanPartyId, MobileParty.PartyObjective.Aggressive));

        Server.Call(() => Assert.Equal(fromOwner, Get<MobileParty>(clanPartyId).Objective == MobileParty.PartyObjective.Aggressive));
    }

    // The owner leads a clan with a second, AI-led party; the outsider plays a party of its own.
    private void SetUp()
    {
        string ownerPartyId = null, outsiderHeroId = null, outsiderPartyId = null;
        Server.Call(() =>
        {
            var ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var owner = ownerParty.LeaderHero;
            var clan = owner.Clan ?? GameObjectCreator.CreateInitializedObject<Clan>();
            owner.Clan = clan;
            clan.SetLeader(owner);
            ownerParty.ActualClan = clan;

            var clanParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            clanParty.LeaderHero.Clan = clan;
            clanParty.ActualClan = clan;

            var outsiderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.NotSame(clan, outsiderParty.LeaderHero.Clan);

            Assert.True(Server.ObjectManager.TryGetId(owner, out ownerHeroId));
            Assert.True(Server.ObjectManager.TryGetId(ownerParty, out ownerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(clanParty, out clanPartyId));
            Assert.True(Server.ObjectManager.TryGetId(clanParty.LeaderHero, out clanPartyLeaderId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty.LeaderHero, out outsiderHeroId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty, out outsiderPartyId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Owner", ownerHeroId, ownerPartyId, clanId, null)));
            Assert.True(playerManager.AddPlayer(new Player("Outsider", outsiderHeroId, outsiderPartyId, null, null)));
            playerManager.SetPeer("Owner", Owner.NetPeer);
            playerManager.SetPeer("Outsider", Outsider.NetPeer);
        });
        environment.FlushCoalescer();
    }

    private void Send<T>(bool fromOwner, T message) where T : Common.Messaging.IMessage
    {
        Server.SimulateMessage((fromOwner ? Owner : Outsider).NetPeer, message);
        Server.PumpGameThread();
    }

    private T Get<T>(string id) where T : class => Server.GetRegisteredObject<T>(id);
}
