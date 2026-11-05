using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

/// <summary>The server applies smithy, clan support and patrol requests only for the sending player's own party and clan.</summary>
public class SmithyClanSupportRequestSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => environment.Clients.First();
    private EnvironmentInstance Outsider => environment.Clients.Last();

    private string ownerHeroId;
    private string ownerPartyId;
    private string clanId;
    private string outsiderClanId;
    private string outsiderHeroId;

    public SmithyClanSupportRequestSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        SetUp();
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Refinement_IsAppliedOnlyForAHeroInTheSendersParty(bool fromOwner)
    {
        AddSmithyWithNoStamina();

        Send(fromOwner, new NetworkDoRefinement(ownerHeroId,
            CraftingMaterials.IronOre, 0, CraftingMaterials.IronOre, 0, CraftingMaterials.Iron1, 0, CraftingMaterials.Iron1, 0));

        Assert.Equal(fromOwner ? 1 : 0, Server.NetworkSentMessages.OfType<NetworkSetHeroCraftingStamina>().Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Smelting_IsAppliedOnlyForAHeroInTheSendersParty(bool fromOwner)
    {
        AddSmithyWithNoStamina();

        Send(fromOwner, new NetworkDoSmelting(ownerHeroId, default));

        Assert.Equal(fromOwner ? 1 : 0, Server.NetworkSentMessages.OfType<NetworkSetHeroCraftingStamina>().Count());
    }

    // The template doesn't exist, so a request that gets past the sender checks ends in the usual failure reply.
    // A refused crafting hero gets that same reply, so the sender's client drops its pending crafted item.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WeaponCrafting_RepliesWithAFailureToTheSender(bool ownHero)
    {
        AddSmithyWithNoStamina();

        Send(fromOwner: true, new NetworkCreateCraftedWeaponInternalServer(true, ownHero ? ownerHeroId : outsiderHeroId,
            new TextObject("Test Sword"), null, "missing_template", "Test Sword", new List<string>(), new List<int>(),
            "", ownerHeroId, null, null, null, "request"));
        Server.Call(() => Server.Resolve<INetwork>().FlushPendingMessages());

        var reply = Assert.Single(Server.NetworkSentMessages.OfType<NetworkCreateCraftedWeaponInternalClients>());
        Assert.False(reply.Success);
        Assert.Equal("request", reply.ClientRequestId);
        Assert.Single(Owner.InternalMessages.OfType<NetworkCreateCraftedWeaponInternalClients>());
        Assert.Empty(Outsider.InternalMessages.OfType<NetworkCreateCraftedWeaponInternalClients>());
    }

    [Fact]
    public void ClanSupportSpendingAnotherClansInfluence_IsRefused()
    {
        Server.Call(() => Get<Clan>(clanId).Influence = 1000f);

        Send(fromOwner: false, new NetworkOnClanSupported(clanId, outsiderClanId));

        Server.Call(() => Assert.Equal(1000f, Get<Clan>(clanId).Influence, precision: 2));
    }

    // A hostile action against a party of a faction at peace declares war, which shows whether it was applied.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PatrolHostileAction_IsAppliedOnlyForTheSendersParty(bool fromOwner)
    {
        string ownerPartyBaseId = null, patrolPartyBaseId = null, patrolClanId = null;
        Server.Call(() =>
        {
            var ownerClan = Get<Clan>(clanId);
            var ownerKingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            ownerClan.Kingdom = ownerKingdom;

            var patrol = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var patrolClan = patrol.LeaderHero.Clan ?? GameObjectCreator.CreateInitializedObject<Clan>();
            patrol.LeaderHero.Clan = patrolClan;
            patrol.ActualClan = patrolClan;
            patrolClan.Kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            Assert.False(FactionManager.IsAtWarAgainstFaction(ownerClan.MapFaction, patrolClan.MapFaction));

            Assert.True(Server.ObjectManager.TryGetId(Get<MobileParty>(ownerPartyId).Party, out ownerPartyBaseId));
            Assert.True(Server.ObjectManager.TryGetId(patrol.Party, out patrolPartyBaseId));
            Assert.True(Server.ObjectManager.TryGetId(patrolClan, out patrolClanId));
        });

        Send(fromOwner, new NetworkPatrolPartyHostileAction(ownerPartyBaseId, patrolPartyBaseId));

        Server.Call(() => Assert.Equal(fromOwner,
            FactionManager.IsAtWarAgainstFaction(Get<Clan>(clanId).MapFaction, Get<Clan>(patrolClanId).MapFaction)));
    }

    // With no stamina left the smithy only reports the unchanged stamina, which is enough to see the request was applied.
    private void AddSmithyWithNoStamina()
    {
        Server.Call(() =>
        {
            var smithy = new CraftingCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { smithy }));
            smithy.SetHeroCraftingStamina(Get<Hero>(ownerHeroId), 0);
        });
        Server.NetworkSentMessages.Clear();
    }

    // The owner leads a clan and plays its party; the outsider plays a party in another clan.
    private void SetUp()
    {
        string outsiderPartyId = null;
        Server.Call(() =>
        {
            var ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var owner = ownerParty.LeaderHero;
            var clan = owner.Clan ?? GameObjectCreator.CreateInitializedObject<Clan>();
            owner.Clan = clan;
            clan.SetLeader(owner);
            ownerParty.ActualClan = clan;

            var outsiderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var outsider = outsiderParty.LeaderHero;
            var outsiderClan = outsider.Clan ?? GameObjectCreator.CreateInitializedObject<Clan>();
            outsider.Clan = outsiderClan;
            outsiderClan.SetLeader(outsider);
            Assert.NotSame(clan, outsiderClan);

            Assert.True(Server.ObjectManager.TryGetId(owner, out ownerHeroId));
            Assert.True(Server.ObjectManager.TryGetId(ownerParty, out ownerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(outsider, out outsiderHeroId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty, out outsiderPartyId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderClan, out outsiderClanId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Owner", ownerHeroId, ownerPartyId, clanId, null)));
            Assert.True(playerManager.AddPlayer(new Player("Outsider", outsiderHeroId, outsiderPartyId, outsiderClanId, null)));
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
