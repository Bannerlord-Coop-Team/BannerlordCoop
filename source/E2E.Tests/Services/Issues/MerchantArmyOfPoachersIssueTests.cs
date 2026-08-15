using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Commands;
using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue;
using Quest = MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;

public class MerchantArmyOfPoachersIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public MerchantArmyOfPoachersIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private (string HeroId, Quest Quest, IssueBase Issue) SetupOwnedQuest()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var villageSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Quest quest = null;
        IssueBase issue = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(villageSettlementId, out var villageSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(townSettlementId, out var townSettlement));

            villageSettlement.SetSettlementComponent(village);
            if (!MBObjectManager.Instance.GetObjectTypeList<Settlement>().Contains(villageSettlement))
            {
                MBObjectManager.Instance.RegisterObject(villageSettlement);
            }

            townSettlement.SetSettlementComponent(new Town());
            hero.StayingInSettlement = townSettlement;

            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

            if (Campaign.Current.PlayerTraitDeveloper == null)
            {
                AccessTools.Property(typeof(Campaign), nameof(Campaign.PlayerTraitDeveloper))
                    .SetValue(Campaign.Current, new PropertyOwner<PropertyObject>());
            }

            if (Hero.MainHero != null && Hero.MainHero.Clan == null)
            {
                Hero.MainHero.Clan = new Clan();
                Hero.MainHero.Clan.SetLeader(Hero.MainHero);
            }

            var giveResult = IssuesDebugCommand.Give(new List<string> { heroId, "MerchantArmyOfPoachers" });
            Assert.True(giveResult.Contains("Gave quest type 'MerchantArmyOfPoachers'"), giveResult);

            issue = hero.Issue;
            quest = Assert.IsType<Quest>(issue.IssueQuest);
        });

        return (heroId, quest, issue);
    }

    [Fact]
    public void QuestSuccessWithPlayerDefeatedPoachers_CapturesTheProof_ValidatorRequiresItThreadedThroughAmbientContext()
    {
        var (_, quest, issue) = SetupOwnedQuest();

        Server.Call(() =>
        {
            quest.QuestSuccessWithPlayerDefeatedPoachers();

            var descriptor = QuestTypeRegistry.Get(issue);
            Assert.NotNull(descriptor?.CaptureQuestSuccessProof);
            Assert.Equal((byte)1, descriptor.CaptureQuestSuccessProof(issue));

            QuestSuccessProofContext.Set(0);
            Assert.False(descriptor.ValidateQuestSuccess(issue, null));

            QuestSuccessProofContext.Set(descriptor.CaptureQuestSuccessProof(issue));
            Assert.True(descriptor.ValidateQuestSuccess(issue, null));
            QuestSuccessProofContext.Set(0);
        });
    }

    [Fact]
    public void QuestSuccessPlayerComesToAnAgreementWithPoachers_CapturesADifferentProofThanTheBattlePath()
    {
        var (_, quest, issue) = SetupOwnedQuest();

        Server.Call(() =>
        {
            quest.QuestSuccessPlayerComesToAnAgreementWithPoachers();

            var descriptor = QuestTypeRegistry.Get(issue);
            Assert.Equal((byte)2, descriptor.CaptureQuestSuccessProof(issue));
        });
    }

    [Fact]
    public void ValidateQuestSuccess_NoProofEverObserved_Rejected()
    {
        var (_, quest, issue) = SetupOwnedQuest();

        Server.Call(() =>
        {
            var descriptor = QuestTypeRegistry.Get(issue);
            QuestSuccessProofContext.Set(0);
            Assert.False(descriptor.ValidateQuestSuccess(issue, null));
        });
    }
}
