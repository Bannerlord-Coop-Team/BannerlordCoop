using Common.Util;
using GameInterface.Services.Issues;
using Common;
using HarmonyLib;
using GameInterface.Services.Issues.Patches;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
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
    public void ClientAmbushCannotReplayWorldCreationEvenInsideAnAcceptanceScope()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var deserters = ObjectHelper.SkipConstructor<MobileParty>();
        var defenders = ObjectHelper.SkipConstructor<MobileParty>();
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest._questGiver = giver;
        quest._defenderMobileParty = defenders;
        var harmony = new Harmony(nameof(ClientAmbushCannotReplayWorldCreationEvenInsideAnAcceptanceScope));
        var wasServer = ModInformation.IsServer;

        try
        {
            ModInformation.IsServer = false;
            harmony.CreateClassProcessor(typeof(ExtortionQuestDefenderCreationPatch)).Patch();
            harmony.CreateClassProcessor(typeof(ExtortionQuestWorldPatches)).Patch();
            using (new ExtortionQuestMirrorScope(giver, deserters))
            using (new AllowedThread())
            {
                quest.StartAmbushEncounter();
                quest.CreateDefenderParty();
            }
            Assert.Same(defenders, quest._defenderMobileParty);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ModInformation.IsServer = wasServer;
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
