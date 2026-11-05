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

    public HeroRequestSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        SetUp();
    }

    public void Dispose() => environment.Dispose();

    // The obituary text needs encyclopedia data the test heroes don't have, so it's skipped like in CompanionDismissalSyncTests.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExecutionAsTheOwnersHero_IsAppliedOnlyFromTheOwner(bool fromOwner)
    {
        var sender = fromOwner ? Owner : Outsider;
        sender.Call(() => sender.Resolve<INetwork>().SendAll(new NetworkExecuteHero(
                captiveId, ownerHeroId, KillCharacterAction.KillCharacterActionDetail.Executed, isForced: true)),
            new[] { AccessTools.Method(typeof(KillCharacterAction), "CreateObituary") });
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(!fromOwner, Get<Hero>(captiveId).IsAlive), new[] { AccessTools.Method(typeof(KillCharacterAction), "CreateObituary") });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TakeHeroIntoTheOwnersParty_IsAppliedOnlyFromTheOwner(bool fromOwner)
    {
        Send(fromOwner ? Owner : Outsider, new MenuTakeHeroToParty(freeHeroId, ownerPartyId));

        Server.Call(() => Assert.Equal(fromOwner, Get<MobileParty>(ownerPartyId) == Get<Hero>(freeHeroId).PartyBelongedTo));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompanionRejoinIntoTheOwnersParty_IsAppliedOnlyFromTheOwner(bool fromOwner)
    {
        Send(fromOwner ? Owner : Outsider, new DoCompanionRejoinAfterEmprisonment(freeHeroId, ownerPartyId));

        Server.Call(() => Assert.Equal(fromOwner, Get<MobileParty>(ownerPartyId) == Get<Hero>(freeHeroId).PartyBelongedTo));
    }

    [Fact]
    public void RescueAnotherClansCompanion_IsRefused()
    {
        Send(Outsider, new RescueCompanion(captiveId));

        Server.Call(() => Assert.True(Get<Hero>(captiveId).IsPrisoner));
    }

    [Fact]
    public void RescueOwnClansCompanion_FreesIt()
    {
        Send(Owner, new RescueCompanion(captiveId));

        Server.Call(() => Assert.False(Get<Hero>(captiveId).IsPrisoner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TakeLordPrisonerIntoTheOwnersParty_IsAppliedOnlyFromTheOwner(bool fromOwner)
    {
        Send(fromOwner ? Owner : Outsider, new NetworkTakeLordPrisoner(ownerPartyBaseId, freeHeroId));

        Server.Call(() => Assert.Equal(fromOwner, Get<Hero>(freeHeroId).IsPrisoner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LordHelpedInBattleAsTheOwnersHero_IsAppliedOnlyFromTheOwner(bool fromOwner)
    {
        Send(fromOwner ? Owner : Outsider, new NetworkLordHelpedInBattle(ownerHeroId, captiveId));

        Server.Call(() => Assert.Equal(!fromOwner, Get<Hero>(captiveId).IsPrisoner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TeleportTheOwnersClanHeroIntoAParty_IsAppliedOnlyFromTheOwner(bool fromOwner)
    {
        Send(fromOwner ? Owner : Outsider, new TeleportHero(freeHeroId, null, ownerPartyId,
            TeleportHeroAction.TeleportationDetail.ImmediateTeleportToParty));

        Server.Call(() => Assert.Equal(fromOwner, Get<MobileParty>(ownerPartyId) == Get<Hero>(freeHeroId).PartyBelongedTo));
    }

    private void SetUp()
    {
        string outsiderHeroId = null, outsiderPartyId = null;
        Server.Call(() =>
        {
            var ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var outsiderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var captorParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var clan = ownerParty.ActualClan;
            Assert.NotSame(clan, outsiderParty.ActualClan);

            // A companion of the owner's clan held by an AI party, and a free companion of the same clan.
            var captive = CreateCompanion(clan);
            TakePrisonerAction.Apply(captorParty.Party, captive);
            var freeHero = CreateCompanion(clan);

            Assert.True(Server.ObjectManager.TryGetId(ownerParty.LeaderHero, out ownerHeroId));
            Assert.True(Server.ObjectManager.TryGetId(ownerParty, out ownerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(ownerParty.Party, out ownerPartyBaseId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty.LeaderHero, out outsiderHeroId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty, out outsiderPartyId));
            Assert.True(Server.ObjectManager.TryGetId(captive, out captiveId));
            Assert.True(Server.ObjectManager.TryGetId(freeHero, out freeHeroId));
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
