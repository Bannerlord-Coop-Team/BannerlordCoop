using Autofac;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic.InstanceResolution;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.Services.Issues.Generic;

/// <summary>
/// Exercises <see cref="OwnershipKeyedInstanceResolution"/>/<see cref="LocationKeyedInstanceResolution"/>'s real
/// selection logic via their <c>ResolveFrom</c> entry points, against
/// real (constructor-skipped) <see cref="QuestBase"/> instances rather than a re-implementation of the
/// selection loop. <see cref="QuestBase"/> is abstract with vanilla-defined abstract members, so a concrete
/// stand-in (<see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest"/>,
/// already used elsewhere in this codebase) is built via <see cref="ObjectHelper.SkipConstructor{T}"/> and its
/// own private <c>_questState</c>/<c>_questGiver</c> fields are force-set by reflection - the same reflection
/// technique this whole design (and this codebase generally) already relies on throughout.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class InstanceResolutionTests : IDisposable
{
    private static readonly Type QuestStatesEnumType = AccessTools.Inner(typeof(QuestBase), "QuestStates");
    private static readonly FieldInfo QuestStateField = AccessTools.Field(typeof(QuestBase), "_questState");
    private static readonly FieldInfo QuestGiverField = AccessTools.Field(typeof(QuestBase), "_questGiver");
    private static readonly object OngoingValue = Enum.Parse(QuestStatesEnumType, "Ongoing");
    private static readonly object FinalizedValue = Enum.Parse(QuestStatesEnumType, "Finalized");

    public InstanceResolutionTests()
    {
        VillageNeedsToolsIssueOwnership.ClearAll();
    }

    public void Dispose()
    {
        VillageNeedsToolsIssueOwnership.ClearAll();
        ContainerProvider.Clear();
    }

    private static Hero NewHero() => ObjectHelper.SkipConstructor<Hero>();

    private static VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest NewQuest(
        Hero questGiver, bool isOngoing)
    {
        var quest = ObjectHelper.SkipConstructor<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>();
        QuestStateField.SetValue(quest, isOngoing ? OngoingValue : FinalizedValue);
        QuestGiverField.SetValue(quest, questGiver);
        return quest;
    }

    private static void SetLocalControllerId(string controllerId)
    {
        var provider = new Mock<IControllerIdProvider>();
        provider.SetupGet(p => p.ControllerId).Returns(controllerId);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(provider.Object).As<IControllerIdProvider>();
        ContainerProvider.SetContainer(builder.Build());
    }

    // --- OwnershipKeyedInstanceResolution ---

    [Fact]
    public void OwnershipKeyed_ReturnsTheOngoingQuestTheLocalPeerOwns()
    {
        var ownedGiver = NewHero();
        VillageNeedsToolsIssueOwnership.SetOwner(ownedGiver, "player-A");
        SetLocalControllerId("player-A");

        var ownedQuest = NewQuest(ownedGiver, isOngoing: true);
        var unownedQuest = NewQuest(NewHero(), isOngoing: true);

        var result = OwnershipKeyedInstanceResolution.ResolveFrom(new[] { unownedQuest, ownedQuest });

        Assert.Same(ownedQuest, result);
    }

    [Fact]
    public void OwnershipKeyed_SkipsAnOwnedQuestThatIsNoLongerOngoing()
    {
        var ownedGiver = NewHero();
        VillageNeedsToolsIssueOwnership.SetOwner(ownedGiver, "player-A");
        SetLocalControllerId("player-A");

        var finalizedOwnedQuest = NewQuest(ownedGiver, isOngoing: false);

        var result = OwnershipKeyedInstanceResolution.ResolveFrom(new[] { finalizedOwnedQuest });

        Assert.Null(result);
    }

    [Fact]
    public void OwnershipKeyed_ReturnsNull_WhenNoCandidateIsLocallyOwned()
    {
        SetLocalControllerId("player-A");
        var quest = NewQuest(NewHero(), isOngoing: true); // never recorded as owned by anyone

        var result = OwnershipKeyedInstanceResolution.ResolveFrom(new[] { quest });

        Assert.Null(result);
    }

    // --- LocationKeyedInstanceResolution ---

    [Fact]
    public void LocationKeyed_ReturnsTheOngoingQuestMatchingCurrentSettlement()
    {
        var settlementA = ObjectHelper.SkipConstructor<Settlement>();
        var settlementB = ObjectHelper.SkipConstructor<Settlement>();
        var questA = NewQuest(NewHero(), isOngoing: true);
        var questB = NewQuest(NewHero(), isOngoing: true);
        var locations = new Dictionary<QuestBase, Settlement> { [questA] = settlementA, [questB] = settlementB };

        var result = LocationKeyedInstanceResolution.ResolveFrom(
            new[] { questA, questB },
            q => locations[q],
            currentSettlement: settlementB,
            preserveVanillaFallback: false);

        Assert.Same(questB, result);
    }

    [Fact]
    public void LocationKeyed_NoFallback_ReturnsNullWhenNoLocationMatches()
    {
        var settlementA = ObjectHelper.SkipConstructor<Settlement>();
        var settlementOther = ObjectHelper.SkipConstructor<Settlement>();
        var questA = NewQuest(NewHero(), isOngoing: true);
        var locations = new Dictionary<QuestBase, Settlement> { [questA] = settlementA };

        // LordNeedsGarrisonTroops' real shape: preserveVanillaFallback=false, __result stays null on no match.
        var result = LocationKeyedInstanceResolution.ResolveFrom(
            new[] { questA }, q => locations[q], currentSettlement: settlementOther, preserveVanillaFallback: false);

        Assert.Null(result);
    }

    [Fact]
    public void LocationKeyed_WithFallback_ReturnsTheFirstOngoingCandidateWhenNoLocationMatches()
    {
        var settlementA = ObjectHelper.SkipConstructor<Settlement>();
        var settlementOther = ObjectHelper.SkipConstructor<Settlement>();
        var firstOngoing = NewQuest(NewHero(), isOngoing: true);
        var secondOngoing = NewQuest(NewHero(), isOngoing: true);
        var locations = new Dictionary<QuestBase, Settlement> { [firstOngoing] = settlementA, [secondOngoing] = settlementA };

        // MerchantArmyOfPoachers' real shape: preserveVanillaFallback=true, falls back to the first ongoing quest.
        var result = LocationKeyedInstanceResolution.ResolveFrom(
            new[] { firstOngoing, secondOngoing }, q => locations[q], currentSettlement: settlementOther, preserveVanillaFallback: true);

        Assert.Same(firstOngoing, result);
    }

    [Fact]
    public void LocationKeyed_SkipsNonOngoingCandidatesEvenForTheFallback()
    {
        var settlementOther = ObjectHelper.SkipConstructor<Settlement>();
        var finalizedQuest = NewQuest(NewHero(), isOngoing: false);
        var ongoingQuest = NewQuest(NewHero(), isOngoing: true);
        var locations = new Dictionary<QuestBase, Settlement>
        {
            [finalizedQuest] = ObjectHelper.SkipConstructor<Settlement>(),
            [ongoingQuest] = ObjectHelper.SkipConstructor<Settlement>(),
        };

        var result = LocationKeyedInstanceResolution.ResolveFrom(
            new[] { finalizedQuest, ongoingQuest }, q => locations[q], currentSettlement: settlementOther, preserveVanillaFallback: true);

        Assert.Same(ongoingQuest, result);
    }

    [Fact]
    public void LocationKeyed_NullCurrentSettlement_NeverMatchesButStillHonorsFallback()
    {
        var quest = NewQuest(NewHero(), isOngoing: true);
        var locations = new Dictionary<QuestBase, Settlement> { [quest] = ObjectHelper.SkipConstructor<Settlement>() };

        var result = LocationKeyedInstanceResolution.ResolveFrom(
            new[] { quest }, q => locations[q], currentSettlement: null, preserveVanillaFallback: true);

        Assert.Same(quest, result);
    }
}
