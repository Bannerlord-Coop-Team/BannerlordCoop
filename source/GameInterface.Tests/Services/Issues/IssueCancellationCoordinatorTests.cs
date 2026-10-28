using Common;
using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Dispatch;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Tests.Bootstrap;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace GameInterface.Tests.Services.Issues;

/// <summary>Checks character changes against the cancellation service without its Harmony callers.</summary>
[Collection(ModInformationRoleCollection.Name)]
public class IssueCancellationCoordinatorTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CharacterChangeCancelsForALivingHeroOrAMissingOriginalParty(bool dead, bool hasParty)
    {
        GameBootStrap.Initialize();
        var previousRole = ModInformation.IsServer;
        var issues = Campaign.Current.IssueManager._issues.ToArray();
        var previousHero = Hero.MainHero;
        var previousParty = MobileParty.MainParty;
        try
        {
            ModInformation.IsServer = true;
            var hero = ObjectHelper.SkipConstructor<Hero>();
            hero._heroState = dead ? Hero.CharacterStates.Dead : Hero.CharacterStates.Active;
            var party = hasParty ? ObjectHelper.SkipConstructor<MobileParty>() : null;
            var quest = new CancellationQuest(hero);
            var issue = ObjectHelper.SkipConstructor<Issue>();
            issue.IssueQuest = quest;
            Campaign.Current.IssueManager._issues.Clear();
            Campaign.Current.IssueManager._issues.Add(hero, issue);
            var registry = new Mock<IQuestTypeRegistry>();
            registry.Setup(value => value.IsRegistered(typeof(Issue))).Returns(true);
            var owners = new Mock<IIssueOwnerResolver>();
            var controller = "player-A";
            owners.Setup(value => value.TryGetOwnerControllerId(issue, out controller)).Returns(true);
            var service = new IssueCancellationCoordinator(registry.Object, owners.Object, Mock.Of<IActiveIssueDismisser>());

            service.CancelForPlayerDeath(controller, hero, party);
            service.CancelForPlayerDeath(controller, hero, party);

            Assert.Equal(1, quest.Cancellations);
            Assert.Same(hero, quest.CancellingHero);
            Assert.Same(party ?? previousParty, quest.CancellingParty);
            Assert.True(quest.IsFinalized);
            Assert.Same(previousHero, Hero.MainHero);
            Assert.Same(previousParty, MobileParty.MainParty);
        }
        finally
        {
            Campaign.Current.IssueManager._issues.Clear();
            foreach (var entry in issues) Campaign.Current.IssueManager._issues.Add(entry.Key, entry.Value);
            ModInformation.IsServer = previousRole;
        }
    }

    /// <summary>Records cancellation under the service's owning-player scope.</summary>
    private sealed class CancellationQuest : QuestBase
    {
        public int Cancellations { get; private set; }
        public Hero CancellingHero { get; private set; }
        public MobileParty CancellingParty { get; private set; }
        public CancellationQuest(Hero giver) : base("character_change", giver, CampaignTime.Never, 0) { }
        public override TextObject Title => new TextObject("Character change");
        public override bool IsRemainingTimeHidden => true;
        public override void SetDialogs() { }
        public override void InitializeQuestOnGameLoad() { }
        public override void OnCanceled()
        {
            Cancellations++;
            CancellingHero = ResolvedMainHeroContext.ResolvedMainHero;
            CancellingParty = MobileParty.MainParty;
        }
    }
}
