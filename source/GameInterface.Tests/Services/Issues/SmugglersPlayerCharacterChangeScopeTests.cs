using Common.Util;
using GameInterface.Services.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class SmugglersPlayerCharacterChangeScopeTests
{
    [Fact]
    public void CharacterReplacementOnlyAffectsOldPlayersQuest()
    {
        var firstPlayer = ObjectHelper.SkipConstructor<Hero>();
        var secondPlayer = ObjectHelper.SkipConstructor<Hero>();
        var firstQuest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var secondQuest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var owners = new SmugglersQuestOwners();
        owners.Set(firstQuest, firstPlayer);
        owners.Set(secondQuest, secondPlayer);

        using (new SmugglersPlayerCharacterChangeScope(firstPlayer))
        {
            Assert.True(SmugglersPlayerCharacterChangeScope.Affects(firstQuest, owners));
            Assert.False(SmugglersPlayerCharacterChangeScope.Affects(secondQuest, owners));
        }
        Assert.False(SmugglersPlayerCharacterChangeScope.IsActive);
    }

    [Fact]
    public void NestedCharacterChangeRestoresOuterPlayer()
    {
        var player = ObjectHelper.SkipConstructor<Hero>();
        var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        var owners = new SmugglersQuestOwners();
        owners.Set(quest, player);

        using (new SmugglersPlayerCharacterChangeScope(player))
        {
            using (new SmugglersPlayerCharacterChangeScope(ObjectHelper.SkipConstructor<Hero>()))
                Assert.False(SmugglersPlayerCharacterChangeScope.Affects(quest, owners));
            Assert.True(SmugglersPlayerCharacterChangeScope.Affects(quest, owners));
        }
        Assert.False(SmugglersPlayerCharacterChangeScope.IsActive);
    }

    [Fact]
    public void UnknownOwnershipCannotCancelAnotherPlayersQuest()
    {
        var quest = ObjectHelper.SkipConstructor<SmugglersIssueBehavior.SmugglersIssueQuest>();
        using (new SmugglersPlayerCharacterChangeScope(ObjectHelper.SkipConstructor<Hero>()))
            Assert.False(SmugglersPlayerCharacterChangeScope.Affects(quest, new SmugglersQuestOwners()));
    }
}
