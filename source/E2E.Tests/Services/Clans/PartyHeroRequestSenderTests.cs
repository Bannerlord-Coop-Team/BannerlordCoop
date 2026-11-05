using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Barbers.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using Common.Network;
using GameInterface.Services.CharacterDevelopers.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Roles;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
    private string ownerPartyId;
    private string clanId;
    private string clanPartyId;
    private string clanPartyLeaderId;

    public PartyHeroRequestSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        SetUp();
    }

    public void Dispose() => environment.Dispose();

    // Only the party leader assigns roles, like the client's clan screen gate. The owner leads the clan but not its AI party.
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void PartyRole_IsSetOnlyByThePartyLeader(bool fromOwner, bool ownersParty, bool applied)
    {
        var partyId = ownersParty ? ownerPartyId : clanPartyId;
        var heroId = ownersParty ? ownerHeroId : clanPartyLeaderId;
        Send(fromOwner, new NetworkSetPartyScout(heroId, partyId));

        Server.Call(() => Assert.Equal(applied, Get<MobileParty>(partyId).Scout == Get<Hero>(heroId)));
    }

    // Every client echoes vanilla's role clear when a party change replicates. The server keeps the roles and refreshes only the sender.
    [Fact]
    public void PartyRoleClearEcho_KeepsRolesAndRefreshesOnlyTheSender()
    {
        Server.Call(() => Get<MobileParty>(clanPartyId).SetPartyScout(Get<Hero>(clanPartyLeaderId)));

        Send(fromOwner: false, new NetworkRemoveAllPartyRolesOfHero(clanPartyLeaderId, clanPartyId));
        Server.Call(() => Server.Resolve<INetwork>().FlushPendingMessages());

        Server.Call(() => Assert.Same(Get<Hero>(clanPartyLeaderId), Get<MobileParty>(clanPartyId).Scout));
        Assert.Single(Outsider.InternalMessages.OfType<NetworkRefreshAfterRoleAssignment>());
        Assert.Empty(Owner.InternalMessages.OfType<NetworkRefreshAfterRoleAssignment>());
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BarberChange_IsAppliedOnlyToTheSendersOwnHero(bool fromOwner)
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

        Send(fromOwner, new NetworkSaveCurrentCharacter(characterId, properties, 0, false));

        Server.Call(() => Assert.Equal(fromOwner, Get<Hero>(ownerHeroId).Weight != weight));
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WageLimit_IsSetOnlyByTheClanLeader(bool fromOwner)
    {
        Server.Call(() => Assert.NotEqual(123, Get<MobileParty>(clanPartyId).PaymentLimit));

        Send(fromOwner, new SetWagePaymentLimit(clanPartyId, 123));

        Server.Call(() => Assert.Equal(fromOwner, Get<MobileParty>(clanPartyId).PaymentLimit == 123));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TownAutoRecruit_IsSetOnlyByTheOwnerClansLeader(bool fromOwner)
    {
        string settlementId = null;
        bool initial = false;
        Server.Call(() =>
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            settlement.Town = town;
            settlement.SetSettlementComponent(town);
            town.OwnerClan = Get<Clan>(clanId);
            town.IsOwnerUnassigned = false;
            initial = town.GarrisonAutoRecruitmentIsEnabled;
            Assert.True(Server.ObjectManager.TryGetId(settlement, out settlementId));
        });

        Send(fromOwner, new ChangeAutoRecruitForSettlement(settlementId, !initial));

        Server.Call(() => Assert.Equal(fromOwner, Get<Settlement>(settlementId).Town.GarrisonAutoRecruitmentIsEnabled != initial));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HeroRename_IsAppliedOnlyToTheSendersOwnHero(bool fromOwner)
    {
        string originalName = null;
        HeroChangeNameData data = null;
        Server.Call(() =>
        {
            var owner = Get<Hero>(ownerHeroId);
            originalName = owner.Name.ToString();
            data = new HeroChangeNameData(owner, new TextObject("Renamed Hero"), new TextObject("Renamed"));
        });

        Send(fromOwner, new NetworkChangeHeroName(data));

        Server.Call(() => Assert.Equal(fromOwner, Get<Hero>(ownerHeroId).Name.ToString() != originalName));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AttributePoints_AreSpentOnlyOnTheSendersOwnHero(bool fromOwner)
    {
        var developerId = environment.CreateRegisteredObject<HeroDeveloper>();
        var attributeId = environment.CreateRegisteredObject<CharacterAttribute>();
        int initial = 0;
        Server.Call(() =>
        {
            var developer = Get<HeroDeveloper>(developerId);
            developer.Hero = Get<Hero>(ownerHeroId);
            developer.UnspentAttributePoints = 5;
            initial = developer.Hero.GetAttributeValue(Get<CharacterAttribute>(attributeId));
        });

        Send(fromOwner, new NetworkApplyChanges(developerId,
            new List<string>(), new List<string> { attributeId }, new List<int> { 1 }, new List<string>(), new List<int>()));

        Server.Call(() => Assert.Equal(fromOwner ? initial + 1 : initial, Get<Hero>(ownerHeroId).GetAttributeValue(Get<CharacterAttribute>(attributeId))));
    }

    // The owner leads a clan with a second, AI-led party; the outsider plays a party of its own.
    private void SetUp()
    {
        string outsiderHeroId = null, outsiderPartyId = null;
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
