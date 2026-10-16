using Common;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Messages;
using Moq;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public class GangLeaderWeaponsPersuasionTests
{
    [Fact]
    public void AuthoritativeResult_RoundTripPreservesProgressAndBlockedOptions()
    {
        var original = new GangLeaderWeaponsPersuasionState(1f, PersuasionOptionResult.Success, new[] { true, false, true });
        var received = ProtoBuf.Serializer.DeepClone(original);
        Assert.Equal(original.Progress, received.Progress);
        Assert.Equal(original.Result, received.Result);
        Assert.Equal(original.BlockedOptions, received.BlockedOptions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BegunOrFailedAttempt_DoesNotAuthorizeDodgingGuards(bool selectFailedOption)
    {
        Run(false, (actions, quest, owner, party) =>
        {
            Assert.True(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.BeginPersuasion));
            if (selectFailedOption)
            {
                Assert.True(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionOption1));
                Assert.Equal(PersuasionOptionResult.Failure, actions.GetPersuasionState(quest).Result);
            }

            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionSucceeded));
            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.BeginPersuasion));
            Assert.True(quest._persuasionTriedOnce);
            Assert.False(quest._playerDodgedGuards);
            Assert.True(quest._guardsParty.IsActive);
        });
    }

    [Fact]
    public void SuccessfulOptions_AreAuthoritativeAndCannotBeRepeatedOrReusedForAnotherEncounter()
    {
        Run(true, (actions, quest, owner, party) =>
        {
            Assert.True(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.BeginPersuasion));
            Assert.True(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionOption1));
            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionOption1));
            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionSucceeded));
            Assert.Equal(1f, actions.GetPersuasionState(quest).Progress);
            Assert.True(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionOption2));
            Assert.Equal(2f, actions.GetPersuasionState(quest).Progress);
            Assert.False(actions.Apply(quest, ObjectHelper.SkipConstructor<Hero>(), party, GangLeaderWeaponsAction.PersuasionSucceeded));

            quest._guardsParty = ObjectHelper.SkipConstructor<MobileParty>();
            quest._guardsParty.IsActive = true;
            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionSucceeded));
            Assert.False(quest._playerDodgedGuards);
        });
    }

    [Fact]
    public void SavedAttemptFlagWithoutServerSession_DoesNotAuthorizeSuccess()
    {
        Run(false, (actions, quest, owner, party) =>
        {
            quest._persuasionTriedOnce = true;
            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionSucceeded));
            Assert.False(actions.Apply(quest, owner, party, GangLeaderWeaponsAction.PersuasionOption1));
            Assert.False(quest._playerDodgedGuards);
        });
    }

    private static void Run(bool successfulOptions, Action<GangLeaderWeaponsQuestActions, Quest, Hero, MobileParty> test)
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
        Exception error = null;
        GameThread.Run(() =>
        {
            var previousCampaign = Campaign.Current;
            var previousGame = Game.Current;
            var wasServer = ModInformation.IsServer;
            try
            {
                ModInformation.IsServer = true;
                Game.Current = ObjectHelper.SkipConstructor<Game>();
                Game.Current.RandomGenerator = new MBFastRandom();
                Game.Current.DefaultSkills = ObjectHelper.SkipConstructor<DefaultSkills>();
                Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
                Campaign.Current.DefaultTraits = ObjectHelper.SkipConstructor<DefaultTraits>();
                Campaign.Current.ConversationManager = ObjectHelper.SkipConstructor<ConversationManager>();
                Campaign.Current.ConversationManager._conversationAgents = new System.Collections.Generic.List<IAgent>();
                Campaign.Current._gameModels = ObjectHelper.SkipConstructor<GameModels>();
                var model = new Mock<PersuasionModel>();
                model.Setup(m => m.CalculatePersuasionGoalValue(It.IsAny<CharacterObject>(), 2f)).Returns(2f);
                float success = successfulOptions ? 1f : 0f, criticalSuccess = 0f, criticalFailure = 0f, failure = successfulOptions ? 0f : 1f;
                model.Setup(m => m.GetChances(It.IsAny<PersuasionOptionArgs>(), out success, out criticalSuccess,
                    out criticalFailure, out failure, It.IsAny<float>()));
                Campaign.Current._gameModels.PersuasionModel = model.Object;
                Campaign.Current.CampaignEventDispatcher = new CampaignEventDispatcher(Array.Empty<CampaignEventReceiver>());

                var owner = ObjectHelper.SkipConstructor<Hero>();
                var party = ObjectHelper.SkipConstructor<MobileParty>();
                var settlement = ObjectHelper.SkipConstructor<Settlement>();
                party._currentSettlement = settlement;
                var quest = ObjectHelper.SkipConstructor<Quest>();
                quest._questState = QuestBase.QuestStates.Ongoing;
                quest.QuestGiver = ObjectHelper.SkipConstructor<Hero>();
                quest.QuestGiver._stayingInSettlement = settlement;
                quest._guardsParty = ObjectHelper.SkipConstructor<MobileParty>();
                quest._guardsParty.IsActive = true;
                test(new GangLeaderWeaponsQuestActions(Mock.Of<IIssueOwnershipRegistry>()), quest, owner, party);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                Campaign.Current = previousCampaign;
                Game.Current = previousGame;
                ModInformation.IsServer = wasServer;
            }
        }, blocking: true);
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
