using Common.Util;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

public class ArtisanOverpricedGoodsIssueTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;

    public ArtisanOverpricedGoodsIssueTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
            instance.Call(() =>
            {
                new IssuesCampaignBehavior().RegisterEvents();
                var honor = DefaultTraits.Honor;
                Assert.True(instance.ObjectManager.AddExisting(honor.StringId, honor,
                    instance.Resolve<TestNetworkRouter>().GetOrCreateFixtureHandle(honor.StringId)));
            });
    }

    public void Dispose()
    {
        environment.Dispose();
    }

    private (string Giver, string Merchant, string Item, string Settlement) CreateSubjects()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var merchantId = environment.CreateRegisteredObject<Hero>();
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        var categoryId = environment.CreateRegisteredObject<ItemCategory>();
        var townId = environment.CreateRegisteredObject<Town>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var clanId = environment.CreateRegisteredObject<Clan>();
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(merchantId, out var merchant));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.True(instance.ObjectManager.TryGetObject<ItemCategory>(categoryId, out var category));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
                using (new AllowedThread())
                {
                    if (town._marketData == null)
                        AccessTools.Field(typeof(Town), "_marketData").SetValue(town, new TownMarketData(town));
                    settlement.SetSettlementComponent(town);
                    Campaign.Current.CampaignObjectManager.Settlements =
                        new MBList<Settlement>(Campaign.Current.CampaignObjectManager.Settlements) { settlement };
                    town.OwnerClan = clan;
                    town.Prosperity = 2500f;
                    giver.StayingInSettlement = settlement;
                    giver.Occupation = Occupation.Artisan;
                    giver.Clan = null;
                    giver._power = 200f;
                    merchant.Occupation = Occupation.Merchant;
                    merchant.Clan = null;
                    merchant._power = 100f;
                    merchant.StayingInSettlement = settlement;
                    merchant.ChangeState(Hero.CharacterStates.Active);
                    settlement.AddHeroWithoutParty(merchant);
                    item.Value = 80;
                    item.StringId = itemId;
                    MBObjectManager.Instance.RegisterObject(item);
                    item.ItemCategory = category;
                    Campaign.Current.Models.TradeItemPriceFactorModel = new StubTradeItemPriceFactorModel(2f);
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                }
            });
        }
        return (giverId, merchantId, itemId, settlementId);
    }

    private void CreateIssue((string Giver, string Merchant, string Item, string Settlement) subjects)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(subjects.Merchant, out var merchant));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(subjects.Item, out var item));
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) => new Issue(owner, merchant, item),
                typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
    }

    private Player ConnectPlayer(EnvironmentInstance client, string controller, string settlementId)
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Player player = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out var characterId));
            player = new Player(controller, heroId, partyId, clanId, characterId);
        });
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(player));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                using (new AllowedThread())
                {
                    party.CurrentSettlement = settlement;
                    hero.StayingInSettlement = settlement;
                    hero.Clan._banner = new Banner();
                    hero.SetBirthDay(CampaignTime.YearsFromNow(-30));
                    hero.Occupation = Occupation.Lord;
                    if (instance == client)
                    {
                        Game.Current.PlayerTroop = hero.CharacterObject;
                        Campaign.Current.MainParty = party;
                        Campaign.Current.PlayerDefaultFaction = hero.Clan;
                        instance.Resolve<IControllerIdProvider>().SetControllerId(controller);
                    }
                }
            });
        }
        environment.ConnectRegisteredPlayer(client, controller);
        return player;
    }

    private void AcceptQuest(EnvironmentInstance client, string giverId, string controller)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, controller));
        });
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            giver.Issue.StartIssueWithQuest();
        });
    }

    [Fact]
    public void SelectingAnHeirCancelsOnlyTheOriginalPlayersDirectQuest()
    {
        // The map switch needs a loaded scene; the real server succession and quest handlers still run.
        using var mapSwitch = new MethodCallRecorder(Priority.First,
            AccessTools.Method(typeof(ChangePlayerCharacterAction), nameof(ChangePlayerCharacterAction.Apply)));
        var first = CreateSubjects();
        var second = CreateSubjects();
        var clients = environment.Clients.ToArray();
        var player = ConnectPlayer(clients[0], "artisan-player-one", first.Settlement);
        var otherPlayer = ConnectPlayer(clients[1], "artisan-player-two", second.Settlement);
        CreateIssue(first);
        CreateIssue(second);
        AcceptQuest(clients[0], first.Giver, player.ControllerId);
        AcceptQuest(clients[1], second.Giver, otherPlayer.ControllerId);
        var heirId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var original));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heirId, out var heir));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            heir.Occupation = Occupation.Lord;
            heir.SetBirthDay(CampaignTime.YearsFromNow(-20));
            heir.Father = original;
            heir.Clan = original.Clan;
            party.MemberRoster.AddToCounts(heir.CharacterObject, 1);
            Server.Resolve<IHeirSelectionCampaignBehaviorInterface>().PrepareSuccession(original);
            original.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedInBattle);
            original.ChangeState(Hero.CharacterStates.Dead);
            Game.Current.PlayerTroop = null;
            Campaign.Current.MainParty = null;
        });
        environment.FlushCoalescer();
        Server.SimulateMessage(clients[0].NetPeer, new NetworkHeirSelectionOver(player.HeroId, heirId), markGameThread: false);
        Server.PumpGameThread();
        environment.FlushCoalescer();
        foreach (var instance in new[] { Server }.Concat(clients))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(player.ControllerId, out var replacement));
                Assert.Equal(heirId, replacement.HeroId);
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(first.Giver, out var giver));
                Assert.Null(giver.Issue);
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(second.Giver, out var otherGiver));
                Assert.True(Assert.IsType<Quest>(otherGiver.Issue.IssueQuest).IsOngoing);
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(otherGiver, out var owner));
                Assert.Equal(otherPlayer.ControllerId, owner);
                Assert.Equal(2500f, giver.CurrentSettlement.Town.Prosperity);
                Assert.Equal(200f, giver.Power);
            });
        }
    }

    [Fact]
    public void TwoClientOwnersAcceptAndPartialDeliveryChangesOnlyTheRequestersQuest()
    {
        var first = CreateSubjects();
        var second = CreateSubjects();
        var clients = environment.Clients.ToArray();
        var player = ConnectPlayer(clients[0], "artisan-player-one", first.Settlement);
        ConnectPlayer(clients[1], "artisan-player-two", second.Settlement);
        CreateIssue(first);
        CreateIssue(second);
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Game.Current.PlayerTroop = null;
                Campaign.Current.MainParty = null;
            }
        });
        AcceptQuest(clients[0], first.Giver, "artisan-player-one");
        AcceptQuest(clients[1], second.Giver, "artisan-player-two");
        foreach (var instance in new[] { Server }.Concat(clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(first.Giver, out var firstGiver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(second.Giver, out var secondGiver));
                Assert.IsType<Quest>(firstGiver.Issue.IssueQuest);
                Assert.IsType<Quest>(secondGiver.Issue.IssueQuest);
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(firstGiver, out var owner));
                Assert.Equal(player.ControllerId, owner);
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(first.Item, out var item));
            party.ItemRoster.AddToCounts(item, 1);
        });
        environment.FlushCoalescer();
        foreach (var client in clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(first.Item, out var item));
                Assert.Equal(1, party.ItemRoster.GetItemNumber(item));
            });
        clients[0].Call(() =>
        {
            Assert.True(clients[0].ObjectManager.TryGetObject<Hero>(first.Giver, out var giver));
            Assert.IsType<Quest>(giver.Issue.IssueQuest).DeliverItemsPartiallyOnConsequence();
        });
        var originalRequest = Assert.Single(clients[0].NetworkSentMessages.GetMessages<RequestArtisanOverpricedGoodsAction>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(first.Item, out var item));
            party.ItemRoster.AddToCounts(item, 1);
        });
        Server.SimulateMessage(clients[0].NetPeer, originalRequest, markGameThread: false);
        Server.SimulateMessage(clients[0].NetPeer,
            new RequestArtisanOverpricedGoodsAction(first.Giver, originalRequest.Generation - 1, 1, ArtisanOverpricedGoodsAction.DeliverPartial), markGameThread: false);
        Server.PumpGameThread();
        environment.FlushCoalescer();
        foreach (var instance in new[] { Server }.Concat(clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(first.Giver, out var firstGiver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(second.Giver, out var secondGiver));
                Assert.Equal(1, Assert.IsType<Quest>(firstGiver.Issue.IssueQuest)._givenTradeGoods);
                Assert.Equal(0, Assert.IsType<Quest>(secondGiver.Issue.IssueQuest)._givenTradeGoods);
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(first.Item, out var item));
                Assert.Equal(1, party.ItemRoster.GetItemNumber(item));
            });
        }
    }

    [Fact]
    public void CreationReplicatesAuthoritativeIdentityDespiteDifferentClientCounters()
    {
        var subjects = CreateSubjects();
        foreach (var client in environment.Clients)
            client.Call(() => Campaign.Current.IssueManager._nextIssueUniqueIndex = 812);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(subjects.Merchant, out var merchant));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(subjects.Item, out var item));
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) => new Issue(owner, merchant, item),
                typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });

        var sent = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkArtisanOverpricedGoodsIssueCreated>());
        Assert.Equal(subjects.Item, sent.ItemId);
        foreach (var client in environment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.Equal(sent.Values.IssueId, issue.StringId);
                Assert.Equal(sent.Values.RewardGold, issue._goldReward);
                Assert.Equal(sent.Values.RequestedAmount, issue.RequestedTradeGoodAmount);
            });
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void AuthoritativeCancellationOrTimeoutPreservesOtherPlayersQuestAndReplicatesTerminalJournal(int outcome, bool disconnected)
    {
        var first = CreateSubjects();
        var second = CreateSubjects();
        var clients = environment.Clients.ToArray();
        var player = ConnectPlayer(clients[0], "artisan-player-one", first.Settlement);
        var otherPlayer = ConnectPlayer(clients[1], "artisan-player-two", second.Settlement);
        CreateIssue(first);
        CreateIssue(second);
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Game.Current.PlayerTroop = null;
                Campaign.Current.MainParty = null;
            }
        });
        AcceptQuest(clients[0], first.Giver, "artisan-player-one");
        AcceptQuest(clients[1], second.Giver, "artisan-player-two");
        var quests = new Dictionary<EnvironmentInstance, Quest>();
        foreach (var instance in new[] { Server }.Concat(clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(first.Giver, out var giver));
                quests[instance] = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                using (new AllowedThread())
                {
                    giver.CurrentSettlement.Town.Prosperity = 2500f;
                    giver._power = 200f;
                }
                if (instance != Server)
                {
                    quests[instance].CompleteQuestWithTimeOut();
                    Assert.True(quests[instance].IsOngoing);
                }
            });
        }
        if (disconnected)
        {
            Server.Resolve<TestNetworkRouter>().Disconnect(clients[0].NetPeer);
            Server.Call(() =>
            {
                var players = Server.Resolve<IPlayerManager>();
                players.ClearPeer(clients[0].NetPeer);
                Assert.True(players.TryGetPlayer(player.ControllerId, out var registered));
                Assert.Same(player, registered);
                Assert.False(players.TryGetPeer(player.ControllerId, out _));
            });
        }
        Server.Call(() =>
        {
            if (outcome == 1) quests[Server].CompleteQuestWithTimeOut();
            else if (outcome == 2)
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var oldHero));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherPlayer.HeroId, out var newHero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherPlayer.MobilePartyId, out var newParty));
                Campaign.Current.QuestManager.OnPlayerCharacterChanged(oldHero, newHero, newParty, false);
            }
            else quests[Server].CompleteQuestWithCancel(new TextObject("{=!}The artisan agreement was canceled."));
        });
        var expectedLogs = quests[Server].JournalEntries.Select(log => log.LogText.ToString()).ToArray();
        Assert.True(expectedLogs.Length >= 2);
        foreach (var instance in new[] { Server }.Concat(clients).Where(instance => !disconnected || instance != clients[0]))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(first.Giver, out var giver));
                Assert.Null(giver.Issue);
                Assert.Equal(outcome == 1 ? 2450f : 2500f, giver.CurrentSettlement.Town.Prosperity);
                Assert.Equal(outcome == 1 ? 180f : 200f, giver.Power);
                Assert.False(quests[instance].IsOngoing);
                Assert.Equal(expectedLogs, quests[instance].JournalEntries.Select(log => log.LogText.ToString()).ToArray());
                Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(second.Giver, out var otherGiver));
                var other = Assert.IsType<Quest>(otherGiver.Issue.IssueQuest);
                Assert.True(other.IsOngoing);
                Assert.Equal(0, other._givenTradeGoods);
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(otherGiver, out var owner));
                Assert.Equal("artisan-player-two", owner);
            });
        }
    }

    [Fact]
    public void DeletedPlayerDoesNotLeaveAnUnresolvablePersonalQuest()
    {
        var subjects = CreateSubjects();
        var otherSubjects = CreateSubjects();
        var clients = environment.Clients.ToArray();
        var player = ConnectPlayer(clients[0], "artisan-player-one", subjects.Settlement);
        var observer = ConnectPlayer(clients[1], "artisan-player-two", otherSubjects.Settlement);
        CreateIssue(subjects);
        CreateIssue(otherSubjects);
        AcceptQuest(clients[0], subjects.Giver, player.ControllerId);
        AcceptQuest(clients[1], otherSubjects.Giver, observer.ControllerId);
        Server.Call(() =>
        {
            Game.Current.PlayerTroop = null;
            Campaign.Current.MainParty = null;
        });

        Server.Call(() => MessageBroker.Instance.Publish(clients[0].NetPeer,
            new NetworkRequestDeletePlayer(player.HeroId, keepConnected: true)),
            new[] { AccessTools.Method(typeof(KillCharacterAction), "CreateObituary") });
        environment.FlushCoalescer();

        foreach (var instance in new[] { Server, clients[1] })
            instance.Call(() =>
            {
                Assert.False(instance.Resolve<IPlayerManager>().TryGetPlayer(player.ControllerId, out _));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                Assert.Null(giver.Issue);
                Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(otherSubjects.Giver, out var otherGiver));
                Assert.True(Assert.IsType<Quest>(otherGiver.Issue.IssueQuest).IsOngoing);
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeletedExpeditionOwnerLeavesNoDisabledCompanionOrPendingReturn(bool alreadyReturning)
    {
        var accepted = AcceptCompanionSolution();
        var clients = environment.Clients.ToArray();
        if (alreadyReturning)
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(accepted.Giver, out var giver));
                giver.Issue.CompleteIssueWithCancel();
                Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(accepted.Player.ControllerId, out _));
            });

        Server.Call(() => MessageBroker.Instance.Publish(clients[0].NetPeer,
            new NetworkRequestDeletePlayer(accepted.Player.HeroId, keepConnected: true)),
            new[] { AccessTools.Method(typeof(KillCharacterAction), "CreateObituary") });
        environment.FlushCoalescer();

        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(accepted.Giver, out var giver));
                Assert.Null(giver.Issue);
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(accepted.Companion, out var companion));
                Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
                Assert.False(instance.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(accepted.Player.ControllerId, out _));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(accepted.Other.HeroId, out var observer));
                Assert.Equal(70000, observer.Gold);
            });
    }

    [Fact]
    public void LoadCancelsOrphanedQuestOnEveryPeerWithoutAnOwnerRegistration()
    {
        var subjects = CreateSubjects();
        var client = environment.Clients.First();
        ConnectPlayer(client, "artisan-player-one", subjects.Settlement);
        CreateIssue(subjects);
        AcceptQuest(client, subjects.Giver, "artisan-player-one");
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                using (new AllowedThread())
                {
                    giver.Issue.IssueQuest = null;
                    Campaign.Current.IssueManager.DeactivateIssue(giver.Issue);
                }
                instance.Resolve<IIssueOwnershipRegistry>().Clear(giver);
                Campaign.Current.QuestManager.OnGameLoaded(null);
                Assert.False(quest.IsOngoing);
                Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullDeliveryPaysOnlyTheOwnerAndRemovesTheQuestOnBothClients(bool crossesTraitThreshold)
    {
        var subjects = CreateSubjects();
        var clients = environment.Clients.ToArray();
        var player = ConnectPlayer(clients[0], "artisan-player-one", subjects.Settlement);
        var otherPlayer = ConnectPlayer(clients[1], "artisan-player-two", subjects.Settlement);
        CreateIssue(subjects);
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Game.Current.PlayerTroop = null;
                Campaign.Current.MainParty = null;
            }
        });
        AcceptQuest(clients[0], subjects.Giver, player.ControllerId);
        var goldBefore = 0;
        var otherGoldBefore = 0;
        var reward = 0;
        var expectedTraitLevel = 0;
        var expectedTraitXp = 0;
        PropertyOwner<PropertyObject> campaignTraits = null;
        var campaignHonorBefore = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherPlayer.HeroId, out var otherHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
            var quest = Assert.IsType<Quest>(giver.Issue.IssueQuest);
            goldBefore = hero.Gold;
            otherGoldBefore = otherHero.Gold;
            reward = quest._rewardGold;
            campaignTraits = Campaign.Current.PlayerTraitDeveloper;
            campaignHonorBefore = campaignTraits?.GetPropertyValue(DefaultTraits.Honor) ?? 0;
            Assert.True(Server.ObjectManager.TryGetHandle(DefaultTraits.Honor, out _), "Honor needs its native registry identity");
            var model = Campaign.Current.Models.CharacterDevelopmentModel;
            var xp = crossesTraitThreshold ? model.GetTraitXpRequiredForTraitLevel(DefaultTraits.Honor, 1) - 50 : 100;
            var progress = new PropertyOwner<PropertyObject>();
            progress.SetPropertyValue(DefaultTraits.Honor, xp);
            GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.Set(hero, progress);
            var otherProgress = new PropertyOwner<PropertyObject>();
            otherProgress.SetPropertyValue(DefaultTraits.Honor, 143);
            GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.Set(otherHero, otherProgress);
            model.GetTraitLevelForTraitXp(hero, DefaultTraits.Honor, xp + 100, out expectedTraitLevel, out expectedTraitXp);
            party.ItemRoster.AddToCounts(quest._requestedTradeGood, quest._requestedTradeGoodAmount);
        });
        clients[0].Call(() =>
        {
            Assert.True(clients[0].ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
            Assert.IsType<Quest>(giver.Issue.IssueQuest).DeliverItemsFullyOnConsequence();
        });
        environment.FlushCoalescer();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherPlayer.HeroId, out var otherHero));
            Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(hero, out var progress));
            Assert.Equal(expectedTraitXp, progress.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(expectedTraitLevel, hero.GetTraitLevel(DefaultTraits.Honor));
            Assert.True(GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(otherHero, out var otherProgress));
            Assert.Equal(143, otherProgress.GetPropertyValue(DefaultTraits.Honor));
            Assert.Same(campaignTraits, Campaign.Current.PlayerTraitDeveloper);
            Assert.Equal(campaignHonorBefore, campaignTraits?.GetPropertyValue(DefaultTraits.Honor) ?? 0);
        });
        foreach (var instance in new[] { Server }.Concat(clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                Assert.Null(giver.Issue);
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(otherPlayer.HeroId, out var otherHero));
                Assert.Equal(goldBefore + reward, hero.Gold);
                Assert.Equal(otherGoldBefore, otherHero.Gold);
                Assert.True(expectedTraitLevel == hero.GetTraitLevel(DefaultTraits.Honor),
                    $"{instance.GetType().Name}: expected Honor {expectedTraitLevel}, actual {hero.GetTraitLevel(DefaultTraits.Honor)}");
                Assert.Equal(0, otherHero.GetTraitLevel(DefaultTraits.Honor));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(subjects.Item, out var item));
                Assert.Equal(0, party.ItemRoster.GetItemNumber(item));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Merchant, out var merchant));
                Assert.Equal(2530f, giver.CurrentSettlement.Town.Prosperity);
                Assert.Equal(210f, giver.Power);
                Assert.Equal(90f, merchant.Power);
                Assert.Equal(5, hero.GetBaseHeroRelation(giver));
                Assert.Equal(-10, hero.GetBaseHeroRelation(merchant));
                Assert.Equal(0, otherHero.GetBaseHeroRelation(giver));
                Assert.Equal(0, otherHero.GetBaseHeroRelation(merchant));
            });
        }
    }

    [Fact]
    public void RefusedLordCounterOfferReplicatesOutcomeAndJournal() => CheckLordCounterOffer(false);

    [Fact]
    public void CompanionAcceptanceChargesAndRemovesTroopsOnlyFromItsOwner() => AcceptCompanionSolution();

    private (string Giver, Player Player, Player Other, string Companion, string Troop, int Needed) AcceptCompanionSolution()
    {
        var subjects = CreateSubjects();
        var clients = environment.Clients.ToArray();
        var player = ConnectPlayer(clients[0], "artisan-player-one", subjects.Settlement);
        var other = ConnectPlayer(clients[1], "artisan-player-two", subjects.Settlement);
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        CreateIssue(subjects);
        var needed = 0;
        var funding = 0;
        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(other.HeroId, out var otherHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                using (new AllowedThread())
                {
                    hero.Gold = 100000;
                    otherHero.Gold = 70000;
                    troop.Level = 20;
                    troop.UpgradeTargets = Array.Empty<CharacterObject>();
                    companion.ChangeState(Hero.CharacterStates.Active);
                    needed = giver.Issue.GetTotalAlternativeSolutionNeededMenCount();
                    funding = Assert.IsType<Issue>(giver.Issue).RequiredGoldForAlternativeSolution;
                    if (instance == Server)
                    {
                        party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                        party.MemberRoster.AddToCounts(troop, needed + 2);
                        Game.Current.PlayerTroop = null;
                        Campaign.Current.MainParty = null;
                    }
                }
            });
        clients[0].Call(() =>
        {
            Assert.True(clients[0].ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
            MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, player.ControllerId));
        });
        clients[0].Call(() =>
        {
            Assert.True(clients[0].ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
            Assert.True(clients[0].ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(clients[0].ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            using (new AllowedThread())
            {
                giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                giver.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, needed);
            }
            giver.Issue.StartIssueWithAlternativeSolution();
        });
        environment.FlushCoalescer();
        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(other.HeroId, out var otherHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party));
                Assert.True(giver.Issue.IsSolvingWithAlternative);
                Assert.Equal(100000 - funding, hero.Gold);
                Assert.Equal(70000, otherHero.Gold);
                Assert.Equal(2, party.MemberRoster.GetTroopCount(troop));
                Assert.False(party.MemberRoster.Contains(companion.CharacterObject));
                Assert.Equal(needed, giver.Issue.AlternativeSolutionSentTroops.GetTroopCount(troop));
                Assert.Same(companion, giver.Issue.AlternativeSolutionHero);
                Assert.Equal(Hero.CharacterStates.Disabled, companion.HeroState);
                Assert.Single(giver.Issue.JournalEntries);
            });
        return (subjects.Giver, player, other, companionId, troopId, needed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionCompletionUsesNativeOutcomeAndReturnsOnlyToOwner(bool failure)
    {
        var accepted = AcceptCompanionSolution();
        var clients = environment.Clients.ToArray();
        var issues = new Dictionary<EnvironmentInstance, Issue>();
        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(accepted.Giver, out var giver));
                issues[instance] = Assert.IsType<Issue>(giver.Issue);
            });
        Server.Call(() =>
        {
            // MBFastRandom.XorShift/NextFloat: x=w=0 produces the source's rare zero roll.
            var random = Game.Current.RandomGenerator;
            var previous = (random._x, random._y, random._z, random._w);
            try
            {
                random._x = 0;
                random._y = 123;
                random._z = 456;
                random._w = failure ? 0u : 100000u;
                Assert.Equal(0f, issues[Server]._failureChance);
                issues[Server].CompleteIssueWithAlternativeSolution();
            }
            finally
            {
                (random._x, random._y, random._z, random._w) = previous;
            }
        });
        environment.FlushCoalescer();
        var expectedLogs = issues[Server].JournalEntries.Select(log => log.LogText.ToString()).ToArray();
        Assert.True(expectedLogs.Length >= 2);
        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(accepted.Giver, out var giver));
                Assert.Null(giver.Issue);
                Assert.Equal(expectedLogs, issues[instance].JournalEntries.Select(log => log.LogText.ToString()).ToArray());
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(accepted.Player.HeroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(accepted.Other.HeroId, out var observer));
                var issue = issues[instance];
                Assert.Equal(failure ? 2500f : 2530f, giver.CurrentSettlement.Town.Prosperity);
                Assert.Equal(failure ? 200f : 210f, giver.Power);
                Assert.Equal(failure ? 100f : 90f, issue.CounterOfferHero.Power);
                Assert.Equal(failure ? 0 : 5, hero.GetBaseHeroRelation(giver));
                Assert.Equal(failure ? 0 : -10, hero.GetBaseHeroRelation(issue.CounterOfferHero));
                Assert.Equal(100000 - issue.RequiredGoldForAlternativeSolution + (failure ? 0 : issue.RewardGold), hero.Gold);
                Assert.Equal(70000, observer.Gold);
                Assert.Equal(0, observer.GetBaseHeroRelation(giver));
                Assert.Equal(0, observer.GetBaseHeroRelation(issue.CounterOfferHero));
                var registry = instance.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
                Assert.False(registry.TryGet(accepted.Other.ControllerId, out _));
                if (instance != clients[1])
                {
                    Assert.True(registry.TryGet(accepted.Player.ControllerId, out var returned));
                    Assert.Equal(accepted.Needed + 1, returned.TotalManCount);
                }
            });
        var outcome = Server.NetworkSentMessages.GetMessages<NetworkArtisanIssueOutcome>().Last();
        Assert.Equal(failure ? IssueBase.IssueUpdateDetails.SentTroopsFailedQuest :
            IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest, outcome.Details);
        InquiryData inquiry = null;
        Action<InquiryData, bool, bool> capture = (data, pause, prioritize) => inquiry = data;
        // Publicizer exposes the event backing field with the same name as the event.
        var inquiryEvent = typeof(InformationManager).GetEvent("OnShowInquiry");
        inquiryEvent.AddEventHandler(null, capture);
        try
        {
            clients[0].Call(() =>
            {
                using (new AllowedThread()) MobileParty.MainParty.IsActive = false;
                Campaign.Current.IssueManager.CheckIfTroopsCanReturnToMainParty();
            });
            Assert.NotNull(inquiry);
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(accepted.Player.HeroId, out var hero));
                hero.HeroState = Hero.CharacterStates.Prisoner;
            });
            clients[0].Call(() => inquiry.AffirmativeAction());
            foreach (var instance in new[] { Server, clients[0] })
                instance.Call(() => Assert.True(instance.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>()
                    .TryGet(accepted.Player.ControllerId, out _)));
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(accepted.Player.HeroId, out var hero));
                hero.HeroState = Hero.CharacterStates.Active;
            });
            environment.FlushCoalescer();
            inquiry = null;
            clients[0].Call(() => Campaign.Current.IssueManager.CheckIfTroopsCanReturnToMainParty());
            Assert.NotNull(inquiry);
            clients[0].Call(() => inquiry.AffirmativeAction());
            clients[0].Call(() => inquiry.AffirmativeAction());
        }
        finally
        {
            inquiryEvent.RemoveEventHandler(null, capture);
        }
        environment.FlushCoalescer();
        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(accepted.Player.MobilePartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(accepted.Companion, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(accepted.Troop, out var troop));
                Assert.Equal(accepted.Needed + 2, party.MemberRoster.GetTroopCount(troop));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
                Assert.False(instance.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(accepted.Player.ControllerId, out _));
            });
    }

    [Fact]
    public void AcceptedLordCounterOfferReplicatesOutcomeAndJournal() => CheckLordCounterOffer(true);

    [Fact]
    public void MerchantCounterOfferUsesTheCurrentMerchantAndPreservesTheOtherQuest()
    {
        var first = CreateSubjects();
        var second = CreateSubjects();
        var clients = environment.Clients.ToArray();
        var player = ConnectPlayer(clients[0], "artisan-player-one", first.Settlement);
        var replacementId = environment.CreateRegisteredObject<Hero>();
        var otherPlayer = ConnectPlayer(clients[1], "artisan-player-two", second.Settlement);
        CreateIssue(first);
        CreateIssue(second);
        AcceptQuest(clients[0], first.Giver, player.ControllerId);
        AcceptQuest(clients[1], second.Giver, "artisan-player-two");
        var quests = new Dictionary<EnvironmentInstance, Quest>();
        var prosperity = 0f;
        var before = new Dictionary<EnvironmentInstance, (int Giver, int Merchant, int OtherGiver, int OtherMerchant, int Gold, int OtherGold)>();
        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(first.Giver, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(first.Merchant, out var oldMerchant));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(replacementId, out var replacement));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(otherPlayer.HeroId, out var observer));
                using (new AllowedThread())
                {
                    oldMerchant.SetTraitLevel(DefaultTraits.Mercy, 1);
                    giver.CurrentSettlement.Town.Prosperity = 2500f;
                    giver._power = 200f;
                    replacement._power = 100f;
                    replacement.SetName(new TextObject("{=!}Current merchant"), new TextObject("{=!}Current merchant"));
                    replacement.Occupation = Occupation.Merchant;
                    replacement.Clan = null;
                    replacement.StayingInSettlement = giver.CurrentSettlement;
                    replacement.ChangeState(Hero.CharacterStates.Active);
                    giver.CurrentSettlement.AddHeroWithoutParty(replacement);
                }
                quests[instance] = Assert.IsType<Quest>(giver.Issue.IssueQuest);
                Assert.Same(replacement, quests[instance].AntagonistHero);
                before[instance] = (hero.GetBaseHeroRelation(giver), hero.GetBaseHeroRelation(replacement),
                    observer.GetBaseHeroRelation(giver), observer.GetBaseHeroRelation(replacement), hero.Gold, observer.Gold);
                if (instance == Server)
                {
                    prosperity = giver.CurrentSettlement.Town.Prosperity;
                    Game.Current.PlayerTroop = null;
                    Campaign.Current.MainParty = null;
                }
            });
        clients[0].Call(() => quests[clients[0]].AcceptCounterOffer());
        environment.FlushCoalescer();
        var logs = quests[Server].JournalEntries.Select(log => log.LogText.ToString()).ToArray();
        Assert.Contains("Current merchant", logs.Last());
        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(first.Giver, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(second.Giver, out var otherGiver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(replacementId, out var merchant));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(otherPlayer.HeroId, out var observer));
                Assert.Null(giver.Issue);
                Assert.Equal(prosperity - 50f, giver.CurrentSettlement.Town.Prosperity);
                Assert.Equal(190f, giver.Power);
                Assert.Equal(105f, merchant.Power);
                Assert.True(before[instance].Giver - 5 == hero.GetBaseHeroRelation(giver),
                    $"Giver relation on {(instance == Server ? "server" : "client")} expected {before[instance].Giver - 5}, actual {hero.GetBaseHeroRelation(giver)}");
                Assert.Equal(before[instance].Merchant + 5, hero.GetBaseHeroRelation(merchant));
                Assert.Equal(before[instance].OtherGiver, observer.GetBaseHeroRelation(giver));
                Assert.Equal(before[instance].OtherMerchant, observer.GetBaseHeroRelation(merchant));
                Assert.Equal(before[instance].Gold, hero.Gold);
                Assert.Equal(before[instance].OtherGold, observer.Gold);
                Assert.Equal(logs, quests[instance].JournalEntries.Select(log => log.LogText.ToString()).ToArray());
                var otherQuest = Assert.IsType<Quest>(otherGiver.Issue.IssueQuest);
                Assert.True(otherQuest.IsOngoing);
                Assert.Equal(0, otherQuest._givenTradeGoods);
            });
    }

    private void CheckLordCounterOffer(bool accept)
    {
        var subjects = CreateSubjects();
        var clients = environment.Clients.ToArray();
        var player = ConnectPlayer(clients[0], "artisan-player-one", subjects.Settlement);
        var observerPlayer = ConnectPlayer(clients[1], "artisan-player-two", subjects.Settlement);
        CreateIssue(subjects);
        var originalGold = 0;
        var originalInfluence = 100f;
        var cost = 0;
        var reward = 0;
        foreach (var instance in new[] { Server }.Concat(clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                using (new AllowedThread())
                {
                    giver.CurrentSettlement.Town.OwnerClan = hero.Clan;
                    hero.Clan.Influence = originalInfluence;
                }
                if (instance == Server)
                {
                    originalGold = hero.Gold;
                    cost = giver.Issue.NeededInfluenceForLordSolution;
                    reward = giver.Issue.RewardGold;
                    Game.Current.PlayerTroop = null;
                    Campaign.Current.MainParty = null;
                }
            });
        }
        clients[0].Call(() =>
        {
            Assert.True(clients[0].ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
            MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, player.ControllerId));
        });
        clients[0].Call(() =>
        {
            Assert.True(clients[0].ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
            giver.Issue.StartIssueWithLordSolution();
        });
        var issues = new Dictionary<EnvironmentInstance, Issue>();
        foreach (var instance in new[] { Server }.Concat(clients))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                issues[instance] = Assert.IsType<Issue>(giver.Issue);
                Assert.True(giver.Issue.IsSolvingWithLordSolution);
            });
        clients[0].Call(() =>
        {
            if (accept) issues[clients[0]].CompleteIssueWithLordSolutionWithAcceptCounterOffer();
            else issues[clients[0]].CompleteIssueWithLordSolutionWithRefuseCounterOffer();
        });
        environment.FlushCoalescer();
        var expectedLogs = issues[Server].JournalEntries.Select(log => log.LogText.ToString()).ToArray();
        Assert.True(expectedLogs.Length >= 2);
        foreach (var instance in new[] { Server }.Concat(clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Giver, out var giver));
                Assert.Null(giver.Issue);
                Assert.Equal(expectedLogs, issues[instance].JournalEntries.Select(log => log.LogText.ToString()).ToArray());
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
                Assert.Equal(originalGold + (accept ? 0 : reward), hero.Gold);
                Assert.Equal(originalInfluence - (accept ? 0 : cost), hero.Clan.Influence);
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(subjects.Merchant, out var merchant));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(observerPlayer.HeroId, out var observer));
                Assert.Equal(accept ? 2470f : 2530f, giver.CurrentSettlement.Town.Prosperity);
                Assert.Equal(accept ? 195f : 210f, giver.Power);
                Assert.Equal(accept ? 100f : 90f, merchant.Power);
                Assert.Equal(accept ? -5 : 5, hero.GetBaseHeroRelation(giver));
                Assert.Equal(accept ? 5 : -10, hero.GetBaseHeroRelation(merchant));
                Assert.Equal(0, observer.GetBaseHeroRelation(giver));
                Assert.Equal(0, observer.GetBaseHeroRelation(merchant));
            });
        }
    }
}
