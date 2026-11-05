using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.GameMenus.Messages;
using GameInterface.Services.Heroes.Messages.LordConversations;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>The server applies a hero request only for the player the sending peer plays.</summary>
public class HeroRequestSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => environment.Clients.First();
    private EnvironmentInstance Outsider => environment.Clients.Last();

    private string ownerHeroId;
    private string ownerPartyId;
    private string ownerPartyBaseId;
    private string captiveId;
    private string freeHeroId;
    private string outsiderHeroId;
    private string otherLordId;

    public HeroRequestSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        SetUp();
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExecuteHero(bool fromOwner)
    {
        // The obituary needs encyclopedia data the test heroes don't have.
        var skipObituary = new[] { AccessTools.Method(typeof(KillCharacterAction), "CreateObituary") };
        var sender = fromOwner ? Owner : Outsider;
        sender.Call(() => sender.Resolve<INetwork>().SendAll(new NetworkExecuteHero(
            captiveId, ownerHeroId, KillCharacterAction.KillCharacterActionDetail.Executed, isForced: true)), skipObituary);
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(!fromOwner, Get<Hero>(captiveId).IsAlive), skipObituary);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void MenuTakeHeroToParty(bool fromOwner, bool ownClanHero, bool applied)
    {
        var heroId = ownClanHero ? freeHeroId : otherLordId;
        Send(fromOwner ? Owner : Outsider, new MenuTakeHeroToParty(heroId, ownerPartyId));

        Server.Call(() => Assert.Equal(applied, Get<MobileParty>(ownerPartyId) == Get<Hero>(heroId).PartyBelongedTo));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void CompanionRejoin(bool fromOwner, bool ownClanHero, bool applied)
    {
        var heroId = ownClanHero ? freeHeroId : otherLordId;
        Send(fromOwner ? Owner : Outsider, new DoCompanionRejoinAfterEmprisonment(heroId, ownerPartyId));

        Server.Call(() => Assert.Equal(applied, Get<MobileParty>(ownerPartyId) == Get<Hero>(heroId).PartyBelongedTo));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RescueCompanion(bool fromOwner)
    {
        Send(fromOwner ? Owner : Outsider, new RescueCompanion(captiveId));

        Server.Call(() => Assert.Equal(!fromOwner, Get<Hero>(captiveId).IsPrisoner));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void TakeLordPrisoner(bool fromOwner, bool playerHero, bool applied)
    {
        var heroId = playerHero ? outsiderHeroId : otherLordId;
        Send(fromOwner ? Owner : Outsider, new NetworkTakeLordPrisoner(ownerPartyBaseId, heroId));

        Server.Call(() => Assert.Equal(applied, Get<Hero>(heroId).IsPrisoner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LordHelpedInBattle(bool fromOwner)
    {
        Send(fromOwner ? Owner : Outsider, new NetworkLordHelpedInBattle(ownerHeroId, captiveId));

        Server.Call(() => Assert.Equal(!fromOwner, Get<Hero>(captiveId).IsPrisoner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TeleportHero(bool fromOwner)
    {
        Send(fromOwner ? Owner : Outsider, new TeleportHero(freeHeroId, null, ownerPartyId,
            TeleportHeroAction.TeleportationDetail.ImmediateTeleportToParty));

        Server.Call(() => Assert.Equal(fromOwner, Get<MobileParty>(ownerPartyId) == Get<Hero>(freeHeroId).PartyBelongedTo));
    }

    private void SetUp()
    {
        string outsiderPartyId = null;
        Server.Call(() =>
        {
            var ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var outsiderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var captorParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var clan = ownerParty.ActualClan;

            // A companion of the owner's clan held by an AI party, a free one, and an AI lord of another clan.
            var captive = CreateCompanion(clan);
            TakePrisonerAction.Apply(captorParty.Party, captive);
            var otherLord = GameObjectCreator.CreateInitializedObject<Hero>();
            Assert.NotSame(clan, otherLord.Clan);

            Assert.True(Server.ObjectManager.TryGetId(ownerParty.LeaderHero, out ownerHeroId));
            Assert.True(Server.ObjectManager.TryGetId(ownerParty, out ownerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(ownerParty.Party, out ownerPartyBaseId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty.LeaderHero, out outsiderHeroId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty, out outsiderPartyId));
            Assert.True(Server.ObjectManager.TryGetId(captive, out captiveId));
            Assert.True(Server.ObjectManager.TryGetId(CreateCompanion(clan), out freeHeroId));
            Assert.True(Server.ObjectManager.TryGetId(otherLord, out otherLordId));
        });
        environment.FlushCoalescer();

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Owner", ownerHeroId, ownerPartyId, null, null)));
            Assert.True(playerManager.AddPlayer(new Player("Outsider", outsiderHeroId, outsiderPartyId, null, null)));
            playerManager.SetPeer("Owner", Owner.NetPeer);
            playerManager.SetPeer("Outsider", Outsider.NetPeer);
        });
    }

    private static Hero CreateCompanion(Clan clan)
    {
        var companion = GameObjectCreator.CreateInitializedObject<Hero>();
        companion.Clan = null;
        companion.SetNewOccupation(Occupation.Wanderer);
        AddCompanionAction.Apply(clan, companion);
        return companion;
    }

    private void Send<T>(EnvironmentInstance sender, T message) where T : Common.Messaging.IMessage
    {
        Server.SimulateMessage(sender.NetPeer, message);
        Server.PumpGameThread();
    }

    private T Get<T>(string id) where T : class => Server.GetRegisteredObject<T>(id);
}
