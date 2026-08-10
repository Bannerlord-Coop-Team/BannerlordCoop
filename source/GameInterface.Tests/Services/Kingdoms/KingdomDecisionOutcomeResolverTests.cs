using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomDecisionOutcomeResolverTests
{
    [Theory]
    [InlineData(typeof(DeclareWarDecision), "DeclareWarDecisionOutcome", "ShouldWarBeDeclared")]
    [InlineData(typeof(MakePeaceKingdomDecision), "MakePeaceDecisionOutcome", "ShouldPeaceBeDeclared")]
    [InlineData(typeof(ExpelClanFromKingdomDecision), "ExpelClanDecisionOutcome", "ShouldBeExpelled")]
    [InlineData(typeof(KingdomPolicyDecision), "PolicyDecisionOutcome", "ShouldDecisionBeEnforced")]
    [InlineData(typeof(SettlementClaimantPreliminaryDecision), "SettlementClaimantPreliminaryOutcome", "ShouldSettlementOwnerChange")]
    [InlineData(typeof(AcceptCallToWarAgreementDecision), "AcceptCallToWarAgreementDecisionOutcome", "ShouldAcceptCallToWar")]
    [InlineData(typeof(ProposeCallToWarAgreementDecision), "ProposeCallToWarAgreementDecisionOutcome", "ShouldCallToWar")]
    [InlineData(typeof(StartAllianceDecision), "StartAllianceDecisionOutcome", "ShouldAllianceBeStarted")]
    [InlineData(typeof(TradeAgreementDecision), "TradeAgreementDecisionOutcome", "ShouldTradeAgreementStart")]
    public void BinaryOutcomeKey_ResolvesMatchingOutcome(Type decisionType, string outcomeTypeName, string fieldName)
    {
        DecisionOutcome yesOutcome = CreateBooleanOutcome(decisionType, outcomeTypeName, true);
        DecisionOutcome noOutcome = CreateBooleanOutcome(decisionType, outcomeTypeName, false);
        var election = ObjectHelper.SkipConstructor<KingdomElection>();
        election._possibleOutcomes = new MBList<DecisionOutcome> { yesOutcome, noOutcome };

        var resolver = new KingdomDecisionOutcomeResolver();

        Assert.True(resolver.TryGetOutcomeKey(noOutcome, null, out string outcomeKey));
        Assert.Contains($"{fieldName}=False", outcomeKey);

        var voteData = new KingdomDecisionVoteData(
            "kingdom",
            0,
            0,
            (int)Supporter.SupportWeights.FullyPush,
            false,
            true,
            outcomeKey);

        Assert.True(resolver.TryGetOutcome(voteData, election, null, out DecisionOutcome resolvedOutcome));
        Assert.Same(noOutcome, resolvedOutcome);
    }

    [Fact]
    public void NonemptyOutcomeKey_DoesNotFallBackToOutcomeIndex()
    {
        DecisionOutcome yesOutcome = CreateBooleanOutcome(typeof(DeclareWarDecision), "DeclareWarDecisionOutcome", true);
        var election = ObjectHelper.SkipConstructor<KingdomElection>();
        election._possibleOutcomes = new MBList<DecisionOutcome> { yesOutcome };
        var voteData = new KingdomDecisionVoteData(
            "kingdom",
            0,
            0,
            (int)Supporter.SupportWeights.FullyPush,
            false,
            true,
            "stale-outcome-key");

        var resolver = new KingdomDecisionOutcomeResolver();

        Assert.False(resolver.TryGetOutcome(voteData, election, null, out DecisionOutcome resolvedOutcome));
        Assert.Null(resolvedOutcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingOutcomeKey_UsesOutcomeIndex(string? outcomeKey)
    {
        DecisionOutcome yesOutcome = CreateBooleanOutcome(typeof(DeclareWarDecision), "DeclareWarDecisionOutcome", true);
        DecisionOutcome noOutcome = CreateBooleanOutcome(typeof(DeclareWarDecision), "DeclareWarDecisionOutcome", false);
        var election = ObjectHelper.SkipConstructor<KingdomElection>();
        election._possibleOutcomes = new MBList<DecisionOutcome> { yesOutcome, noOutcome };
        var voteData = new KingdomDecisionVoteData(
            "kingdom",
            0,
            1,
            (int)Supporter.SupportWeights.FullyPush,
            false,
            true,
            outcomeKey);

        var resolver = new KingdomDecisionOutcomeResolver();

        Assert.True(resolver.TryGetOutcome(voteData, election, null, out DecisionOutcome resolvedOutcome));
        Assert.Same(noOutcome, resolvedOutcome);
    }

    [Fact]
    public void ClaimantOutcomeKey_ResolvesClanWhenLocalIndexDiffers()
    {
        var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
        var firstClan = ObjectHelper.SkipConstructor<TaleWorlds.CampaignSystem.Clan>();
        firstClan.StringId = "first";
        var selectedClan = ObjectHelper.SkipConstructor<TaleWorlds.CampaignSystem.Clan>();
        selectedClan.StringId = "selected";
        objectManager.AddExisting(firstClan.StringId, firstClan);
        objectManager.AddExisting(selectedClan.StringId, selectedClan);
        var firstOutcome = new SettlementClaimantDecision.ClanAsDecisionOutcome(firstClan);
        var selectedOutcome = new SettlementClaimantDecision.ClanAsDecisionOutcome(selectedClan);
        var election = ObjectHelper.SkipConstructor<KingdomElection>();
        election._possibleOutcomes = new MBList<DecisionOutcome> { firstOutcome, selectedOutcome };
        var resolver = new KingdomDecisionOutcomeResolver();

        Assert.True(resolver.TryGetOutcomeKey(selectedOutcome, objectManager, out string outcomeKey));
        var voteData = new KingdomDecisionVoteData(
            "kingdom",
            0,
            0,
            (int)Supporter.SupportWeights.FullyPush,
            false,
            true,
            outcomeKey);

        Assert.True(resolver.TryGetOutcome(voteData, election, objectManager, out DecisionOutcome resolvedOutcome));
        Assert.Same(selectedOutcome, resolvedOutcome);
    }

    private static DecisionOutcome CreateBooleanOutcome(Type decisionType, string outcomeTypeName, bool value)
    {
        Type outcomeType = decisionType.GetNestedType(
            outcomeTypeName,
            BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new NullReferenceException($"{outcomeTypeName} outcome type was not found.");

        ConstructorInfo constructor = outcomeType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(info => info.GetParameters().Length > 0 && info.GetParameters()[0].ParameterType == typeof(bool));
        object?[] args = constructor.GetParameters()
            .Select((_, index) => index == 0 ? (object)value : null)
            .ToArray();

        return Assert.IsAssignableFrom<DecisionOutcome>(constructor.Invoke(args));
    }
}
