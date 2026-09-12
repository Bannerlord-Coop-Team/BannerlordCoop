using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class AlternativeSolutionCompletionOwnerResolutionTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientB => TestEnvironment.Clients.ElementAt(1);

    public AlternativeSolutionCompletionOwnerResolutionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void CompleteOnServer_TrueOwnerIsRemoteClient_RewardGoldAndTroopsLandOnTrueOwnerNotServerMainHero()
    {
        var controllerId = "player-B-" + Guid.NewGuid();

        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var trueOwnerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var trueOwnerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(trueOwnerHeroId, out var trueOwner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(trueOwnerPartyId, out var trueOwnerParty));

            using (new AllowedThread())
            {
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                settlement.SetSettlementComponent(village);
                village.Bound = settlement;
                village.Hearth = 650f;
                owner.StayingInSettlement = settlement;
                owner.Occupation = Occupation.RuralNotable;
                AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value)).SetValue(item, 40);
                companion.ChangeState(Hero.CharacterStates.Disabled);
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, trueOwnerHeroId, trueOwnerPartyId, "", "")));
        });

        TestEnvironment.ConnectRegisteredPlayer(ClientB, controllerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(itemId, out var requestedItem));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                IssueBase.IssueFrequency.VeryCommon);

            using (new AllowedThread())
            {
                Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            }

            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));

            using (new AllowedThread())
            using (new AlternativeSolutionStartAuthorityGuard())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.StartIssueWithAlternativeSolution();
            }
        });

        int serverMainHeroGoldBefore = 0;
        CharacterObject serverMainHeroCharacterBefore = null;
        int trueOwnerGoldBefore = 0;
        Server.Call(() =>
        {
            serverMainHeroGoldBefore = Hero.MainHero.Gold;
            serverMainHeroCharacterBefore = Hero.MainHero.CharacterObject;

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(trueOwnerHeroId, out var trueOwner));
            trueOwnerGoldBefore = trueOwner.Gold;
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            var issue = (IssueBase)owner.Issue;

            using (new AllowedThread())
            {
                AlternativeSolutionCompletionRunner.CompleteOnServer(owner, issue);
            }
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(trueOwnerHeroId, out var trueOwner));

            Assert.True(trueOwner.Gold > trueOwnerGoldBefore);
            Assert.Equal(serverMainHeroGoldBefore, Hero.MainHero.Gold);
            Assert.Equal(serverMainHeroCharacterBefore, Hero.MainHero.CharacterObject);

            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var deposited));
            Assert.True(deposited.TotalManCount >= 1);
        });
    }
}
