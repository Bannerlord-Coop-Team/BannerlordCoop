using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class GangLeaderNeedsToOffloadStolenGoodsIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public GangLeaderNeedsToOffloadStolenGoodsIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private void OpenConversation(EnvironmentInstance instance, string ownerId, string controllerId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            MessageBroker.Instance.Publish(owner, new IssueConversationOpenedLocally(owner, controllerId));
        });
    }

    private static readonly FieldInfo ResolvedMainHeroField =
        AccessTools.Field(Type.GetType("GameInterface.Services.Heroes.Patches.ResolvedMainHeroContext, GameInterface"), "ResolvedMainHero");

    private static void SetResolvedMainHero(Hero hero) => ResolvedMainHeroField.SetValue(null, hero);

    private static readonly PropertyInfo ItemValueProperty =
        AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value));
    private static readonly PropertyInfo PlayerProgressProperty =
        AccessTools.Property(typeof(Campaign), nameof(Campaign.PlayerProgress));
    private static readonly FieldInfo RandomForStolenTradeGoodField =
        AccessTools.Field(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), "_randomForStolenTradeGood");
    private static readonly PropertyInfo TradeItemPriceFactorModelProperty =
        AccessTools.Property(typeof(GameModels), nameof(GameModels.TradeItemPriceFactorModel));

    private static readonly FieldInfo TownMarketDataField =
        AccessTools.Field(typeof(Town), "_marketData");

    private static void EnsureMarketData(Town town)
    {
        if (TownMarketDataField.GetValue(town) == null)
        {
            TownMarketDataField.SetValue(town, new TownMarketData(town));
        }
    }

    private const string StolenGoodId = "jewelry";

    private record GangLeaderFixture(
        string HeroId,
        string OwnerSettlementId,
        string IssueHideoutSettlementId,
        string CounterOfferHeroId);

    private GangLeaderFixture SetupIssueOwner(int stolenGoodValue = 100)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownerTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var ownerSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var ownerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        var issueHideoutId = TestEnvironment.CreateRegisteredObject<Hideout>();
        var issueHideoutSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        var counterOfferHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var stolenGoodCategoryId = TestEnvironment.CreateRegisteredObject<ItemCategory>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(ownerTownId, out var ownerTown));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(ownerSettlementId, out var ownerSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));

                Assert.True(instance.ObjectManager.TryGetObject<Hideout>(issueHideoutId, out var issueHideout));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(issueHideoutSettlementId, out var issueHideoutSettlement));

                Assert.True(instance.ObjectManager.TryGetObject<Hero>(counterOfferHeroId, out var counterOfferHero));
                Assert.True(instance.ObjectManager.TryGetObject<ItemCategory>(stolenGoodCategoryId, out var stolenGoodCategory));

                using (new AllowedThread())
                {
                    EnsureMarketData(ownerTown);

                    ownerSettlement.SetSettlementComponent(ownerTown);
                    ownerTown.OwnerClan = ownerClan;
                    hero.StayingInSettlement = ownerSettlement;
                    hero.Occupation = Occupation.GangLeader;
                    Campaign.Current.MainParty = ownerParty;

                    hero.Clan.SetLeader(hero);
                    counterOfferHero.Clan.SetLeader(counterOfferHero);

                    if (Campaign.Current.Models.TradeItemPriceFactorModel is not StubTradeItemPriceFactorModel)
                    {
                        TradeItemPriceFactorModelProperty.SetValue(Campaign.Current.Models, new StubTradeItemPriceFactorModel());
                    }

                    if (Campaign.Current.GameMenuManager.GetGameMenu("hideout_place") == null)
                    {
                        var hideoutMenu = new GameMenu("hideout_place");
                        hideoutMenu.Initialize(new TextObject("Hideout"), null, GameMenu.MenuOverlayType.None);
                        Campaign.Current.GameMenuManager.AddGameMenu(hideoutMenu);
                    }

                    issueHideoutSettlement.SetSettlementComponent(issueHideout);

                    var requiredBanditParties = System.Math.Max(1,
                        Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt);
                    for (var i = 0; i < requiredBanditParties; i++)
                    {
                        var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
                        banditClan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
                        var banditParty = BanditPartyComponent.CreateBanditParty(
                            $"E2EGangLeaderStolenGoodsBandit{i}",
                            banditClan,
                            issueHideout,
                            isBossParty: false,
                            pt: null,
                            new CampaignVec2(Vec2.Zero, true));
                        banditParty.CurrentSettlement = issueHideoutSettlement;
                    }
                    Assert.True(issueHideout.IsInfested);

                    counterOfferHero.Occupation = Occupation.Merchant;
                    ownerSettlement.AddHeroWithoutParty(counterOfferHero);
                    counterOfferHero.StayingInSettlement = ownerSettlement;
                    counterOfferHero.ChangeState(Hero.CharacterStates.Active);

                    var stolenGood = GameObjectCreator.CreateInitializedObject<ItemObject>();
                    ItemValueProperty.SetValue(stolenGood, stolenGoodValue);
                    stolenGood.ItemCategory = stolenGoodCategory;
                    Assert.True(instance.ObjectManager.AddExisting(StolenGoodId, stolenGood));

                    stolenGood.StringId = StolenGoodId;
                    MBObjectManager.Instance.RegisterObject(stolenGood);

                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
                }
            });
        }

        return new GangLeaderFixture(heroId, ownerSettlementId, issueHideoutSettlementId, counterOfferHeroId);
    }

    private void CreateIssueOnServer(GangLeaderFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.IssueHideoutSettlementId, out var issueHideoutSettlement));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                {
                    var issue = new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(h, issueHideoutSettlement);
                    RandomForStolenTradeGoodField.SetValue(issue, 0);
                    return issue;
                },
                typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    [Fact]
    public void GenuineServerCreation_CapturesTheRolledFieldsAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.IssueHideoutSettlementId, created.IssueHideoutId);
        Assert.Equal(0, created.RandomForStolenTradeGood);
        Assert.Equal(fixture.CounterOfferHeroId, created.CounterOfferHeroId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Settlement>(fixture.IssueHideoutSettlementId, out var issueHideoutSettlement));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));

                Assert.Same(issueHideoutSettlement, mirrored._issueHideout);
                Assert.Equal(0, mirrored._randomForStolenTradeGood);
                Assert.Same(counterOfferHero, mirrored.CounterOfferHero);
            });
        }
    }

    [Fact]
    public void ClientOriginatedCreation_IsBlocked_IssueManagerNeverCreatesIt()
    {
        var fixture = SetupIssueOwner();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Settlement>(fixture.IssueHideoutSettlementId, out var issueHideoutSettlement));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(h, issueHideoutSettlement),
                typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsIssueCreated>());
    }

    [Fact]
    public void RemoteClientAccept_ForceCorrectsPriceAmountRewardAndCounterOfferGold_OnEveryPeer_IncludingTheAccepterItself_WhenPriceAndDifficultyDiverge()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.OwnerSettlementId, out var settlement));
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");

        Server.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 1.0f));
        Client.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.1f));
        OtherClient.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.55f));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 400); }
        });
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 50); }
        });
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 150); }
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        Assert.Single(Client.InternalMessages.GetMessages<QuestTypeQuestSolutionAcceptTriggered>());

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        var acceptedFields = GenericAcceptFieldsSerializer.Deserialize<GangLeaderStolenGoodsQuestAcceptFields>(accepted.FieldsBytes);
        Assert.True(acceptedFields.StolenTradeGoodAmount > 0);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var quest = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
            Assert.Equal(acceptedFields.StolenTradeGoodAmount, quest._stolenTradeGoodAmount);
            Assert.Equal(acceptedFields.StolenTradeGoodPrice, quest._stolenTradeGoodPrice);
            Assert.Equal(acceptedFields.RewardGold, quest.RewardGold);
            Assert.Equal(acceptedFields.CounterOfferGold, quest._counterOfferGold);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue.IssueQuest);
                Assert.True(owner.Issue.IsSolvingWithQuest);
            });
        }

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestQuestTypeAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.OwnerSettlementId, out var settlement));
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
            Assert.True(playerManager.AddPlayer(new Player("player-B", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        TestEnvironment.ConnectRegisteredPlayer(OtherClient, "player-B");
        OpenConversation(Client, fixture.HeroId, "player-A");

        var generation = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, generation));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        OpenConversation(OtherClient, fixture.HeroId, "player-B");
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, generation));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkQuestTypeAcceptRejected>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue.IssueQuest);
                Assert.True(owner.Issue.IsSolvingWithQuest);
                Assert.True(client.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestQuestTypeAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, 0));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });
    }

    [Fact]
    public void HideoutOwnershipGate_BlocksSuccessAndFailConsequencesForAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("host-controller", fixture.HeroId, null, "", "")));
        });

        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            quest.FailQuestByLosingHideoutBattle();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.SucceedQuestByPayingAndGivingTheGoodsBack();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.FailQuestByGivingBackTheGoods();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.FailQuestByKeepingTheGoods();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.OnSettlementLeft(PartyBase.MainParty.MobileParty, quest.QuestGiver.CurrentSettlement);

            quest.SucceedQuestByPayingAndKeepingTheGoods();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            Assert.Equal(0, PartyBase.MainParty.ItemRoster.GetItemNumber(quest._stolenTradeGood));
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            SetResolvedMainHero(owner);

            using (new AllowedThread())
            using (new IssueFinalizeAuthorityGuard())
            {
                quest.SucceedQuestByPayingAndKeepingTheGoods();
            }

            Assert.Null(owner.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            Assert.True(PartyBase.MainParty.ItemRoster.GetItemNumber(quest._stolenTradeGood) > 0);
        });
    }

    [Fact]
    public void AlternativeSolutionPayoutFreeze_CapturesTheAuthoritativeAmountAndRewardOnceAndForceWritesThemOntoEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var escortTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.OwnerSettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out var escortTroop));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var companion));
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                escortTroop.Level = 15;
                party.MemberRoster.AddToCounts(escortTroop, 20);
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");
        OtherClient.Call(() => Assert.True(OtherClient.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out _)));

        Server.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 1.0f));
        Client.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.1f));
        OtherClient.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.55f));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 400); }
        });
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 50); }
        });
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 150); }
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(escortTroopId, out var escortTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(escortTroop, 20);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Assert.Single(Client.InternalMessages.GetMessages<QuestTypeAlternativeAcceptTriggered>());

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        var acceptedPayload = GenericAcceptFieldsSerializer.Deserialize<GangLeaderStolenGoodsAlternativeAcceptPayload>(accepted.FieldsBytes);
        Assert.True(acceptedPayload.StolenTradeGoodAmount > 0);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var issue = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue>(owner.Issue);
                Assert.Equal(acceptedPayload.StolenTradeGoodAmount, issue.StolenTradeGoodAmount);
                Assert.Equal(acceptedPayload.RewardGold, issue.RewardGold);
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
                using (new AllowedThread()) { ItemValueProperty.SetValue(item, 9999); }

                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var issue = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue>(owner.Issue);
                Assert.Equal(acceptedPayload.StolenTradeGoodAmount, issue.StolenTradeGoodAmount);
                Assert.Equal(acceptedPayload.RewardGold, issue.RewardGold);
            });
        }
    }

    [Fact]
    public void RequestIssueRemoved_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.NotNull(owner.Issue);
            Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                using (new AllowedThread())
                {
                    owner.Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                }
                Assert.Null(owner.Issue.IssueQuest);
                Assert.True(owner.Issue.IsSolvingWithQuest);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestCancel, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestCancel, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
                Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            });
        }
    }
}
