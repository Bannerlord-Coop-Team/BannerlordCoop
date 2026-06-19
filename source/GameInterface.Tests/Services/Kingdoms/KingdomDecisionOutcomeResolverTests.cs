using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
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
    [InlineData(typeof(KingdomPolicyDecision), "PolicyDecisionOutcome", "<ShouldDecisionBeEnforced>k__BackingField")]
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

        Assert.True(KingdomDecisionOutcomeResolver.TryGetOutcomeKey(noOutcome, null, out string outcomeKey));
        Assert.Contains($"{fieldName}=False", outcomeKey);

        var voteData = new KingdomDecisionVoteData(
            "kingdom",
            0,
            0,
            (int)Supporter.SupportWeights.FullyPush,
            false,
            true,
            outcomeKey);

        Assert.True(KingdomDecisionOutcomeResolver.TryGetOutcome(voteData, election, null, out DecisionOutcome resolvedOutcome));
        Assert.Same(noOutcome, resolvedOutcome);
    }

    private static DecisionOutcome CreateBooleanOutcome(Type decisionType, string outcomeTypeName, bool value)
    {
        Type outcomeType = decisionType.GetNestedType(
            outcomeTypeName,
            BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(outcomeType);

        ConstructorInfo constructor = outcomeType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(info => info.GetParameters().Length > 0 && info.GetParameters()[0].ParameterType == typeof(bool));
        object[] args = constructor.GetParameters()
            .Select((_, index) => index == 0 ? (object)value : null)
            .ToArray();

        return Assert.IsAssignableFrom<DecisionOutcome>(constructor.Invoke(args));
    }
}
