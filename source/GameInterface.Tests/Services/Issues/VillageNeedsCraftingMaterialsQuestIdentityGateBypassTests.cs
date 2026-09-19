using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;
using GameInterface.Services.Issues.Patches;
using GameInterface.Tests;
using HarmonyLib;
using Moq;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public class VillageNeedsCraftingMaterialsQuestIdentityGateBypassTests : IDisposable
{
    private static readonly FieldInfo QuestGiverField = AccessTools.Field(typeof(QuestBase), "_questGiver");

    public void Dispose()
    {
        ContainerProvider.Clear();
        ModInformation.IsServer = false;
    }

    private static Hero NewHero() => ObjectHelper.SkipConstructor<Hero>();

    private static Quest NewQuestFor(Hero giver)
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        QuestGiverField.SetValue(quest, giver);
        return quest;
    }

    private static void SetUpNonOwningPeer(Hero giver, string recordedOwnerControllerId, string localControllerId)
    {
        var registry = new IssueOwnershipRegistry();
        registry.SetOwner(giver, recordedOwnerControllerId);

        var controllerIdProvider = new Mock<IControllerIdProvider>();
        controllerIdProvider.SetupGet(p => p.ControllerId).Returns(localControllerId);

        var syncPolicy = new Mock<ISyncPolicy>();
        syncPolicy.Setup(p => p.AllowOriginal()).Returns(false);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(controllerIdProvider.Object).As<IControllerIdProvider>();
        builder.RegisterInstance((IIssueOwnershipRegistry)registry).As<IIssueOwnershipRegistry>();
        builder.RegisterInstance(syncPolicy.Object).As<ISyncPolicy>();
        ContainerProvider.SetContainer(builder.Build());
    }

    [Fact]
    public void OnWarDeclaredGate_AnOpenAllowedThreadNeverOverridesAResolvedNonOwner()
    {
        var giver = NewHero();
        SetUpNonOwningPeer(giver, "player-A", "player-B");
        var quest = NewQuestFor(giver);
        var faction = ObjectHelper.SkipConstructor<Clan>();

        bool result;
        using (new AllowedThread())
        {
            result = VillageNeedsCraftingMaterialsQuestWarDeclaredGatePatch.Prefix(
                quest, faction, faction, DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
        }

        Assert.False(result);
    }

    [Fact]
    public void OnClanChangedKingdomGate_AnOpenAllowedThreadNeverOverridesAResolvedNonOwner()
    {
        var giver = NewHero();
        SetUpNonOwningPeer(giver, "player-A", "player-B");
        var quest = NewQuestFor(giver);

        bool result;
        using (new AllowedThread())
        {
            result = VillageNeedsCraftingMaterialsQuestClanChangedKingdomGatePatch.Prefix(
                quest, null, null, null, default, false);
        }

        Assert.False(result);
    }

    [Fact]
    public void OnMapEventStartedGate_AnOpenAllowedThreadNeverOverridesAResolvedNonOwner()
    {
        var giver = NewHero();
        SetUpNonOwningPeer(giver, "player-A", "player-B");
        var quest = NewQuestFor(giver);

        bool result;
        using (new AllowedThread())
        {
            result = VillageNeedsCraftingMaterialsQuestMapEventStartedGatePatch.Prefix(quest, null, null, null);
        }

        Assert.False(result);
    }

    [Fact]
    public void RaidCompletedGate_AnOpenAllowedThreadOnANonServerPeerNeverOpensTheAuthorityGuard()
    {
        var syncPolicy = new Mock<ISyncPolicy>();
        syncPolicy.Setup(p => p.AllowOriginal()).Returns(false);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(syncPolicy.Object).As<ISyncPolicy>();
        ContainerProvider.SetContainer(builder.Build());
        ModInformation.IsServer = false;

        bool result;
        IssueFinalizeAuthorityGuard state;
        using (new AllowedThread())
        {
            result = VillageNeedsCraftingMaterialsQuestRaidCompletedAuthorityPatch.Prefix(out state);
        }

        Assert.False(result);
        Assert.Null(state);
        Assert.False(IssueFinalizeAuthorityGuard.IsActive);
    }
}
