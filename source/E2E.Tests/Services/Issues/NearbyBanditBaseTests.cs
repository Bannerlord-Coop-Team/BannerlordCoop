using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
using SandBox.GauntletUI.Map;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

namespace E2E.Tests.Services.Issues;

public class NearbyBanditBaseTests : IDisposable
{
    private static readonly MethodBase[] QuestLogTexts =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestSucceededLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestFailedLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestCanceledLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestFailedFromWarCausedByPlayerLogText)),
        AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog)),
    };

    private static readonly MethodBase[] QuestAcceptedMethods =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestStartedLogText)),
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
        AccessTools.Method(typeof(QuestHelper), nameof(QuestHelper.AddMapArrowFromPointToTarget)),
        AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)),
        AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog)),
    };

    private static readonly MethodBase[] MapEventMethods =
    {
        AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
    };

    private readonly string headmanId;
    private readonly string villageSettlementId;
    private readonly string townSettlementId;
    private readonly string hideoutSettlementId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public NearbyBanditBaseTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        headmanId = TestEnvironment.CreateRegisteredObject<Hero>();
        villageSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        townSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        hideoutSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var hideoutId = TestEnvironment.CreateRegisteredObject<Hideout>();

        (ownerHeroId, ownerPartyId) = RegisterPlayer(ClientA, "player-A");
        RegisterPlayer(ClientB, "player-B");

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(villageSettlementId, out var villageSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townSettlementId, out var townSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideoutSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Hideout>(hideoutId, out var hideout));

                using (new AllowedThread())
                {
                    townSettlement.SetSettlementComponent(town);
                    villageSettlement.Village = village;
                    villageSettlement.SetSettlementComponent(village);
                    village.Bound = townSettlement;
                    hideoutSettlement.SetSettlementComponent(hideout);
                    headman.StayingInSettlement = villageSettlement;
                    headman.Occupation = Occupation.Headman;
                }
            });
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void EveryClientBuildsTheIssueWithTheServersHideoutAndTheSameVillage()
    {
        CreateIssueOnServer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(villageSettlementId, out var village));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
                var issue = Assert.IsType<Issue>(headman.Issue);

                Assert.Same(hideout, issue._targetHideout);
                Assert.Same(village, issue._issueSettlement);
            });
        }
    }

    [Fact]
    public void AcceptingSpotsTheHideoutOnTheServerAndEveryClient()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
                Assert.True(hideout.Hideout.IsSpotted);
            });
        }
    }

    [Fact]
    public void AHideoutBattleTheOwnerWinsPaysTheOwnerOnceAndEndsTheQuestEverywhere()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var goldBefore = GoldOfOwner(Server);

        EndHideoutBattleOnServer(ownerPartyId, BattleState.AttackerVictory);
        TestEnvironment.FlushCoalescer();

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestSuccess, finalized.Outcome);
        Assert.Equal(goldBefore + 3000, GoldOfOwner(Server));
        Assert.Equal(goldBefore + 3000, GoldOfOwner(ClientA));
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void AHideoutClearedWithoutTheOwnerFailsTheOwnersQuest()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var goldBefore = GoldOfOwner(Server);

        EndHideoutBattleOnServer(otherPartyId, BattleState.AttackerVictory);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        Assert.Equal(goldBefore, GoldOfOwner(Server));
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void ATimeOutChangesTheHeadmansPowerOnlyOnce()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var powerBefore = PowerOfHeadman(Server);

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            var quest = Assert.IsType<Quest>(headman.Issue.IssueQuest);

            quest.CompleteQuestWithTimeOut();
        }, QuestLogTexts);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestTimeOut, finalized.Outcome);
        Assert.Equal(powerBefore - 5f, PowerOfHeadman(Server));
        Assert.Equal(powerBefore - 5f, PowerOfHeadman(ClientA));
    }

    [Fact]
    public void TheOwnersAcceptStartsTheQuestWithoutSpottingTheHideoutOnTheClient()
    {
        CreateIssueOnServer();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.True(ClientA.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            headman.Issue.StartIssueWithQuest();
            var quest = Assert.IsType<Quest>(headman.Issue.IssueQuest);

            using (new AllowedThread())
            {
                hideout.Hideout.IsSpotted = false;
            }

            quest.OnQuestAccepted();

            Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
            Assert.True(hideout.IsVisible);
            Assert.False(hideout.Hideout.IsSpotted);
        }, QuestAcceptedMethods);
    }

    [Fact]
    public void AHideoutBattleTheOwnerLosesFailsTheQuestEverywhere()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var powerBefore = PowerOfHeadman(Server);

        EndHideoutBattleOnServer(ownerPartyId, BattleState.DefenderVictory);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        Assert.Equal(powerBefore - 5f, PowerOfHeadman(Server));
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void AnEmptiedHideoutCancelsTheQuestEverywhere()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            CampaignEventDispatcher.Instance.OnHideoutDeactivated(hideout);
        }, QuestLogTexts);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void AWarTheOwnerCausesFailsTheQuestEverywhere()
    {
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();

        CreateIssueOnServer();
        AcceptOnClientA();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));

            DeclareWarAction.ApplyInternal(kingdom, clan, DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
        }, QuestLogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void TheServerKeepsAiLordsAwayWhileAPlayerIsHeldInTheHideout()
    {
        CreateIssueOnServer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
            var issue = Assert.IsType<Issue>(headman.Issue);

            Assert.True(issue.CanBeCompletedByAI());

            using (new AllowedThread())
            {
                hideout.Party.PrisonRoster.AddToCounts(owner.CharacterObject, 1);
            }

            Assert.False(issue.CanBeCompletedByAI());
        });
    }

    private (string HeroId, string PartyId) RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                hero._partyBelongedTo = party;
                hero.CharacterObject.HeroObject = hero;
            }

            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        return (heroId, partyId);
    }

    private void CreateIssueOnServer()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            var potentialIssue = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) => new Issue(hero, hideout),
                typeof(Issue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, headman));
        });
    }

    private void AcceptOnClientA()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));

            headman.Issue.StartIssueWithQuest();

            using (new AllowedThread())
            {
                headman.Issue.IssueQuest.StartQuest();
            }
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.IsType<Quest>(headman.Issue.IssueQuest);
        });
    }

    private void EndHideoutBattleOnServer(string attackerPartyId, BattleState battleState)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            mapEvent.Initialize(attacker.Party, hideout.Party, new HideoutEventComponent(mapEvent, false), MapEvent.BattleTypes.Hideout);
            mapEvent.MapEventVisual = null;
            mapEvent._battleState = battleState;

            CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
        }, MapEventMethods.Concat(QuestLogTexts));
    }

    private void AssertIssueEndedEverywhere()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
                Assert.Null(headman.Issue);
            });
        }
    }

    private int GoldOfOwner(EnvironmentInstance instance)
    {
        var gold = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            gold = owner.Gold;
        });

        return gold;
    }

    private float PowerOfHeadman(EnvironmentInstance instance)
    {
        var power = 0f;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            power = headman.Power;
        });

        return power;
    }
}
