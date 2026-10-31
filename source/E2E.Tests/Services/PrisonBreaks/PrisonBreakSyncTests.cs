using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.PrisonBreaks.Handlers;
using HarmonyLib;
using System.Reflection;
using GameInterface.Services.PrisonBreaks.Interfaces;
using GameInterface.Services.PrisonBreaks.Messages;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PrisonBreaks;

public class PrisonBreakSyncTests : IDisposable
{
    private const int StartingGold = 100000;
    private const string RescuerControllerId = "Rescuer";

    private readonly E2ETestEnvironment testEnvironment;
    private EnvironmentInstance Server => testEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => testEnvironment.Clients;
    private EnvironmentInstance Requester => testEnvironment.Clients.First();

    public PrisonBreakSyncTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose() => testEnvironment.Dispose();

    [Fact]
    public void StartRequest_TakesServerBribeAndStartsCooldownEverywhere()
    {
        var fixture = CreateFixture();

        SendStart(fixture);

        Assert.Equal(PrisonBreakStartResult.Approved, GetStartResult());
        Server.Call(() =>
        {
            var (playerHero, settlement, prisoner) = Resolve(Server, fixture);
            Assert.Equal(StartingGold - GetBribeCost(playerHero, prisoner), playerHero.Gold);
            Assert.True(Server.Resolve<IPrisonBreakCampaignBehaviorInterface>().IsOnCooldown(settlement));
        });
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var (_, settlement, _) = Resolve(client, fixture);
                Assert.True(client.Resolve<IPrisonBreakCampaignBehaviorInterface>().IsOnCooldown(settlement));
                Assert.True(client.Resolve<IPrisonBreakCampaignBehaviorInterface>().IsPrisonBreakActive(settlement));
            });
        }
    }

    [Fact]
    public void StartRequest_OnCooldown_IsRejectedWithoutTakingGoldAgain()
    {
        var fixture = CreateFixture();

        SendStart(fixture);
        SendStart(fixture);

        var results = Server.NetworkSentMessages.OfType<NetworkPrisonBreakStartResolved>().Select(message => message.Result);
        Assert.Equal(new[] { PrisonBreakStartResult.Approved, PrisonBreakStartResult.OnCooldown }, results);
        Server.Call(() =>
        {
            var (playerHero, _, prisoner) = Resolve(Server, fixture);
            Assert.Equal(StartingGold - GetBribeCost(playerHero, prisoner), playerHero.Gold);
        });
    }

    [Fact]
    public void StartRequest_WithoutEnoughGold_IsRejected()
    {
        var fixture = CreateFixture(gold: 0);

        SendStart(fixture);

        Assert.Equal(PrisonBreakStartResult.NotEnoughGold, GetStartResult());
        Server.Call(() =>
        {
            var (_, settlement, _) = Resolve(Server, fixture);
            Assert.False(Server.Resolve<IPrisonBreakCampaignBehaviorInterface>().IsOnCooldown(settlement));
        });
    }

    [Fact]
    public void StartRequest_ForPlayerHeroPrisoner_IsRejected()
    {
        var fixture = CreateFixture();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.PrisonerId, out var prisoner));
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
            Server.Resolve<IPlayerManager>().AddPlayer(new Player("PrisonerPlayer", fixture.PrisonerId, partyId, "MyClanId", "MyCharacterObjectId"));
        });

        SendStart(fixture);

        Assert.Equal(PrisonBreakStartResult.PrisonerUnavailable, GetStartResult());
    }

    [Fact]
    public void EndRequest_Succeeded_FreesPrisonerAndClearsActiveBreak()
    {
        var fixture = CreateFixture();
        SendStart(fixture);

        SendEnd(PrisonBreakOutcome.Succeeded);

        Server.Call(() =>
        {
            var (_, _, prisoner) = Resolve(Server, fixture);
            Assert.False(prisoner.IsPrisoner);
        });
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var (_, settlement, prisoner) = Resolve(client, fixture);
                Assert.False(prisoner.IsPrisoner);
                Assert.False(client.Resolve<IPrisonBreakCampaignBehaviorInterface>().IsPrisonBreakActive(settlement));
            });
        }
    }

    [Fact]
    public void EndRequest_PlayerCaptured_ImprisonsPlayerInSettlement()
    {
        var fixture = CreateFixture();
        SendStart(fixture);

        SendEnd(PrisonBreakOutcome.PlayerCaptured);

        Server.Call(() =>
        {
            var (playerHero, settlement, prisoner) = Resolve(Server, fixture);
            Assert.True(playerHero.IsPrisoner);
            Assert.Same(settlement.Party, playerHero.PartyBelongedToAsPrisoner);
            Assert.True(prisoner.IsPrisoner);
        });
    }

    [Fact]
    public void EndRequest_WithoutRunningBreak_IsIgnored()
    {
        var fixture = CreateFixture();
        SendStart(fixture);
        SendEnd(PrisonBreakOutcome.PrisonerLeftBehind);
        float rogueryXp = 0;
        Server.Call(() => rogueryXp = Resolve(Server, fixture).PlayerHero.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery));

        SendEnd(PrisonBreakOutcome.Succeeded);

        Server.Call(() =>
        {
            var (playerHero, _, prisoner) = Resolve(Server, fixture);
            Assert.True(prisoner.IsPrisoner);
            Assert.Equal(rogueryXp, playerHero.HeroDeveloper.GetSkillXp(DefaultSkills.Roguery));
        });
    }

    [Fact]
    public void PrisonerReleasedDuringBreak_NotifiesRescuerAndClearsActiveBreak()
    {
        var fixture = CreateFixture();
        SendStart(fixture);
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            var (_, _, prisoner) = Resolve(Server, fixture);
            EndCaptivityAction.ApplyByReleasedByChoice(prisoner);
            Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick());
        });
        testEnvironment.FlushCoalescer();

        var lost = Assert.Single(Server.NetworkSentMessages.OfType<NetworkPrisonBreakPrisonerLost>());
        Assert.Equal(fixture.PrisonerId, lost.PrisonerId);
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var (_, settlement, _) = Resolve(client, fixture);
                Assert.False(client.Resolve<IPrisonBreakCampaignBehaviorInterface>().IsPrisonBreakActive(settlement));
            });
        }
    }

    [Fact]
    public void PlayerJoiningDuringBreak_ReceivesActiveBreak()
    {
        var fixture = CreateFixture();
        SendStart(fixture);
        var lateClient = Clients.Last();

        // Stands in for a client that joined after the start broadcast.
        lateClient.Call(() => lateClient.Resolve<IPrisonBreakCampaignBehaviorInterface>().SetPrisonBreakActive(Resolve(lateClient, fixture).Settlement, false));

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerConnectionStateChanged()));
        Server.PumpGameThread();
        testEnvironment.FlushCoalescer();

        lateClient.Call(() =>
        {
            var (_, settlement, _) = Resolve(lateClient, fixture);
            Assert.True(lateClient.Resolve<IPrisonBreakCampaignBehaviorInterface>().IsPrisonBreakActive(settlement));
        });
    }

    private Fixture CreateFixture(int gold = StartingGold)
    {
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() => Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[]
            {
                new PrisonBreakCampaignBehavior(),
            })));
        }

        Fixture fixture = default;
        Server.Call(() =>
        {
            var playerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            var prisoner = GameObjectCreator.CreateInitializedObject<Hero>();
            var playerHero = playerParty.LeaderHero;

            settlement.Town = town;
            settlement.SetSettlementComponent(town);
            town.OwnerClan = GameObjectCreator.CreateInitializedObject<Clan>();
            if (gold > 0) GiveGoldAction.ApplyBetweenCharacters(null, playerHero, gold - playerHero.Gold, false);
            TakePrisonerAction.Apply(settlement.Party, prisoner);

            Assert.True(prisoner.IsPrisoner);
            Assert.True(Server.ObjectManager.TryGetId(playerHero, out var playerHeroId));
            Assert.True(Server.ObjectManager.TryGetId(playerParty, out var playerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(settlement, out var settlementId));
            Assert.True(Server.ObjectManager.TryGetId(prisoner, out var prisonerId));
            Server.Resolve<IPlayerManager>().AddPlayer(new Player(RescuerControllerId, playerHeroId, playerPartyId, "MyClanId", "MyCharacterObjectId"));
            fixture = new Fixture(playerHeroId, settlementId, prisonerId);
        });
        testEnvironment.FlushCoalescer();
        testEnvironment.ConnectRegisteredPlayer(Requester, RescuerControllerId);
        Server.NetworkSentMessages.Clear();

        return fixture;
    }

    // The requester's reply handler opens the local mission, or cancels because the test client isn't in the settlement.
    private static readonly MethodBase[] StartReplyDisabled =
    {
        AccessTools.Method(typeof(PrisonBreakHandler), "Handle_NetworkPrisonBreakStartResolved"),
    };

    private void SendStart(Fixture fixture)
    {
        Requester.Call(() => Requester.Resolve<INetwork>().SendAll(
            new NetworkRequestPrisonBreakStart(fixture.PlayerHeroId, fixture.SettlementId, fixture.PrisonerId)), StartReplyDisabled);
        testEnvironment.FlushCoalescer();
    }

    private void SendEnd(PrisonBreakOutcome outcome)
    {
        Requester.Call(() => Requester.Resolve<INetwork>().SendAll(new NetworkRequestPrisonBreakEnd(outcome)));
        testEnvironment.FlushCoalescer();
    }

    private PrisonBreakStartResult GetStartResult() =>
        Assert.Single(Server.NetworkSentMessages.OfType<NetworkPrisonBreakStartResolved>()).Result;

    private static int GetBribeCost(Hero playerHero, Hero prisoner)
    {
        using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
        {
            return Campaign.Current.Models.PrisonBreakModel.GetPrisonBreakStartCost(prisoner);
        }
    }

    private static (Hero PlayerHero, Settlement Settlement, Hero Prisoner) Resolve(EnvironmentInstance instance, Fixture fixture)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.PlayerHeroId, out var playerHero));
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.PrisonerId, out var prisoner));
        return (playerHero, settlement, prisoner);
    }

    private readonly record struct Fixture(string PlayerHeroId, string SettlementId, string PrisonerId);
}
