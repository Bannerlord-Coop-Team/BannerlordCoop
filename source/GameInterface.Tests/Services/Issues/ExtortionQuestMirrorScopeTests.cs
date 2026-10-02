using Common.Util;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Patches;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

public class ExtortionQuestMirrorScopeTests
{
    [Fact]
    public void MirroringBindsTheExistingPartyAndSkipsVanillaCreation()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest._questGiver = giver;

        using (new ExtortionQuestMirrorScope(giver, party))
        {
            Assert.False(ExtortionQuestCreationPatches.Prefix(quest));
            Assert.Same(party, quest._deserterMobileParty);
        }

        Assert.False(ExtortionQuestMirrorScope.IsActive);
    }

    [Fact]
    public void AmbushBindsTheServerDefenderAndRejectsAnAcceptanceOnlyScope()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var deserters = ObjectHelper.SkipConstructor<MobileParty>();
        var defenders = ObjectHelper.SkipConstructor<MobileParty>();
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest._questGiver = giver;

        using (new ExtortionQuestMirrorScope(giver, deserters))
            Assert.Throws<InvalidOperationException>(() => ExtortionQuestMirrorScope.ApplyDefender(quest));
        Assert.Null(quest._defenderMobileParty);

        using (new ExtortionQuestMirrorScope(giver, deserters, defenders))
        {
            ExtortionQuestMirrorScope.ApplyDefender(quest);
            Assert.Same(defenders, quest._defenderMobileParty);
        }
    }

    [Fact]
    public void AnotherQuestCannotConsumeTheMirroredParty()
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest._questGiver = ObjectHelper.SkipConstructor<Hero>();
        using (new ExtortionQuestMirrorScope(ObjectHelper.SkipConstructor<Hero>(), ObjectHelper.SkipConstructor<MobileParty>()))
        {
            Assert.Throws<InvalidOperationException>(() => ExtortionQuestMirrorScope.Apply(quest));
            Assert.Null(quest._deserterMobileParty);
        }
    }

    [Fact]
    public void FailedNestedMirrorRestoresTheOuterPartyBinding()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest._questGiver = giver;

        using (new ExtortionQuestMirrorScope(giver, party))
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (new ExtortionQuestMirrorScope(ObjectHelper.SkipConstructor<Hero>(), ObjectHelper.SkipConstructor<MobileParty>()))
                {
                    ExtortionQuestMirrorScope.Apply(quest);
                }
            });
            ExtortionQuestMirrorScope.Apply(quest);
            Assert.Same(party, quest._deserterMobileParty);
        }

        Assert.Throws<InvalidOperationException>(() => ExtortionQuestMirrorScope.Apply(quest));
    }
}
