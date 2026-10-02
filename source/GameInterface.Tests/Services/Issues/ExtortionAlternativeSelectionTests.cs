using Common.Messaging;
using Common.Util;
using Autofac;
using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Patches;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;

[Collection(ModInformationRoleCollection.Name)]
public class ExtortionAlternativeSelectionTests
{
    public ExtortionAlternativeSelectionTests() => GameBootStrap.Initialize();

    [Fact]
    public void SelectingTroopsPreservesTheRealPartyAndCommitsOnlyUpgrades()
    {
        var companion = ObjectHelper.SkipConstructor<Hero>();
        companion._characterObject = new CharacterObject();
        companion.CharacterObject.HeroObject = companion;
        var upgraded = new CharacterObject();
        var troop = new CharacterObject { UpgradeTargets = new[] { upgraded } };
        var realRoster = TroopRoster.CreateDummyTroopRoster();
        realRoster.AddToCounts(companion.CharacterObject, 1);
        realRoster.AddToCounts(troop, 9, xpChange: 90);
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue._issueState = IssueBase.IssueState.Ongoing;
        var broker = new Mock<IMessageBroker>(MockBehavior.Strict);
        var selection = new ExtortionAlternativeSelection(broker.Object, new Mock<IControllerIdProvider>().Object);
        selection.Begin(issue, companion);
        var logic = new PartyScreenLogic();
        var data = new PartyScreenLogicInitializationData
        {
            LeftMemberRoster = selection.SelectedTroops,
            RightMemberRoster = realRoster,
            RightPrisonerRoster = TroopRoster.CreateDummyTroopRoster()
        };

        selection.PrepareScreen(logic, ref data);
        logic.MemberRosters[0] = data.LeftMemberRoster;
        logic.MemberRosters[1] = data.RightMemberRoster;
        logic._initialData.RightMemberRoster = data.RightMemberRoster.CloneRosterData();
        selection.HideSelectedCompanion(logic);
        var wasServer = ModInformation.IsServer;
        var models = Campaign.Current._gameModels;
        var upgradeModel = new Mock<PartyTroopUpgradeModel>();
        upgradeModel.Setup(model => model.GetXpCostForUpgrade(It.IsAny<PartyBase>(), troop, upgraded)).Returns(10);
        var harmony = new Harmony(nameof(SelectingTroopsPreservesTheRealPartyAndCommitsOnlyUpgrades));
        var builder = new ContainerBuilder();
        builder.RegisterInstance(selection);
        using var container = builder.Build();
        try
        {
            ModInformation.IsServer = false;
            ContainerProvider.SetContainer(container);
            Campaign.Current._gameModels = new GameModels(new List<GameModel>
            {
                new DefaultCharacterStatsModel(), upgradeModel.Object
            });
            harmony.CreateClassProcessor(typeof(ExtortionAlternativeScreenPatch)).Patch();
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right,
                PartyScreenLogic.TroopType.Member, troop, 6, 0, -1);
            logic.TransferTroop(command, false);
            Assert.Equal(30, logic.MemberRosters[1].GetElementXp(troop));
            Assert.Equal(60, logic.MemberRosters[0].GetElementXp(troop));
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Left,
                PartyScreenLogic.TroopType.Member, troop, 2, 0, -1);
            logic.TransferTroop(command, false);
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right,
                PartyScreenLogic.TroopType.Member, troop, 2, 0, -1);
            logic.TransferTroop(command, false);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ContainerProvider.Clear();
            ModInformation.IsServer = wasServer;
            Campaign.Current._gameModels = models;
        }
        logic.MemberRosters[1].AddToCounts(troop, -2, xpChange: -20);
        logic.MemberRosters[1].AddToCounts(upgraded, 2);

        var commit = selection.GetCommitRoster(logic);

        Assert.Equal(1, commit.GetTroopCount(companion.CharacterObject));
        Assert.Equal(7, commit.GetTroopCount(troop));
        Assert.Equal(2, commit.GetTroopCount(upgraded));
        Assert.Equal(1, realRoster.GetTroopCount(companion.CharacterObject));
        Assert.Equal(9, realRoster.GetTroopCount(troop));
        Assert.Equal(9, logic._initialData.RightMemberRoster.GetTroopCount(troop));
        Assert.Equal(6, selection.SelectedTroops.GetTroopCount(troop));
        Assert.Equal(70, commit.GetElementXp(troop));
        Assert.Equal(90, realRoster.GetElementXp(troop));
        Assert.Equal(60, selection.SelectedTroops.GetElementXp(troop));
        selection.Clear();
        Assert.False(selection.IsSelecting);
        Assert.Equal(9, realRoster.GetTroopCount(troop));
        broker.VerifyNoOtherCalls();
    }

    [Fact]
    public void ResettingPrivateSelectionCannotDuplicateTheCompanion()
    {
        var companion = ObjectHelper.SkipConstructor<Hero>();
        companion._characterObject = new CharacterObject();
        companion.CharacterObject.HeroObject = companion;
        var realRoster = TroopRoster.CreateDummyTroopRoster();
        realRoster.AddToCounts(companion.CharacterObject, 1);
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue._issueState = IssueBase.IssueState.Ongoing;
        var selection = new ExtortionAlternativeSelection(new Mock<IMessageBroker>(MockBehavior.Strict).Object,
            new Mock<IControllerIdProvider>().Object);
        selection.Begin(issue, companion);
        var logic = new PartyScreenLogic();
        var data = new PartyScreenLogicInitializationData
        {
            LeftMemberRoster = selection.SelectedTroops,
            RightMemberRoster = realRoster,
            RightPrisonerRoster = TroopRoster.CreateDummyTroopRoster()
        };
        selection.PrepareScreen(logic, ref data);
        logic.MemberRosters[0] = data.LeftMemberRoster;
        logic.MemberRosters[1] = data.RightMemberRoster;
        logic.CurrentData.BindRostersFrom(data.RightMemberRoster, data.RightPrisonerRoster,
            data.LeftMemberRoster, TroopRoster.CreateDummyTroopRoster(), null, null);
        logic._initialData.InitializeCopyFrom(null, null);
        logic._initialData.CopyFromScreenData(logic.CurrentData);
        selection.HideSelectedCompanion(logic);

        logic.CurrentData.ResetUsing(logic._initialData);

        Assert.Equal(1, selection.GetCommitRoster(logic).GetTroopCount(companion.CharacterObject));
        Assert.False(selection.CanAccept(issue));
        Assert.Equal(1, realRoster.GetTroopCount(companion.CharacterObject));
    }

    [Fact]
    public void StaleSelectionCannotAcceptAnotherIssueOrACurrentWinner()
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue._issueState = IssueBase.IssueState.Ongoing;
        var companion = ObjectHelper.SkipConstructor<Hero>();
        companion._characterObject = new CharacterObject();
        var broker = new Mock<IMessageBroker>(MockBehavior.Strict);
        var selection = new ExtortionAlternativeSelection(broker.Object, new Mock<IControllerIdProvider>().Object);
        selection.Begin(issue, companion);
        Assert.True(selection.Matches(issue));

        selection.Accept(ObjectHelper.SkipConstructor<Issue>());
        Assert.False(selection.IsSelecting);
        selection.Begin(issue, companion);
        issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
        selection.Accept(issue);

        Assert.False(selection.IsSelecting);
        broker.VerifyNoOtherCalls();
    }
}
