using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomDecisionOutcomeOrderTests
{
    [Fact]
    public void ResolveOrderedOutcomes_DifferentLocalSetAndOrder_ConvergesToServerKeys()
    {
        var objectManager = new Mock<IObjectManager>();
        DecisionOutcome fenBeannis = CreateClanOutcome("clan_fen_beannis", objectManager);
        DecisionOutcome pethros = CreateClanOutcome("clan_pethros", objectManager);
        DecisionOutcome mesui = CreateClanOutcome("clan_mesui", objectManager);
        DecisionOutcome localOnly = CreateClanOutcome("clan_local_only", objectManager);
        var resolver = new KingdomDecisionOutcomeResolver();
        var order = new KingdomDecisionOutcomeOrder(resolver);

        string[] serverKeys = order.CaptureKeys(new[] { fenBeannis, pethros, mesui }, objectManager.Object);
        IReadOnlyList<DecisionOutcome> resolved = order.ResolveOrderedOutcomes(
            serverKeys,
            new[] { pethros, localOnly },
            new[] { mesui, fenBeannis, pethros, localOnly },
            objectManager.Object);

        Assert.Equal(serverKeys, order.CaptureKeys(resolved, objectManager.Object));
        Assert.Same(fenBeannis, resolved[0]);
        Assert.Same(pethros, resolved[1]);
        Assert.Same(mesui, resolved[2]);
        Assert.DoesNotContain(localOnly, resolved);
    }

    [Fact]
    public void CaptureKeys_SkipsOutcomesWithoutStableKeys()
    {
        var order = new KingdomDecisionOutcomeOrder(new KingdomDecisionOutcomeResolver());
        DecisionOutcome keyed = CreateBooleanOutcome(true);

        string[] keys = order.CaptureKeys(new[] { keyed, null }, null);

        Assert.Single(keys);
        Assert.Contains("ShouldWarBeDeclared=True", keys[0]);
    }

    [Fact]
    public void ResolveOrderedOutcomes_MissingServerCandidate_DoesNotApplyPartialSet()
    {
        var order = new KingdomDecisionOutcomeOrder(new KingdomDecisionOutcomeResolver());
        DecisionOutcome local = CreateBooleanOutcome(true);

        IReadOnlyList<DecisionOutcome> resolved = order.ResolveOrderedOutcomes(
            new[]
            {
                "TaleWorlds.CampaignSystem.Election.DeclareWarDecision+DeclareWarDecisionOutcome:ShouldWarBeDeclared=True",
                "TaleWorlds.CampaignSystem.Election.DeclareWarDecision+DeclareWarDecisionOutcome:ShouldWarBeDeclared=False",
            },
            new[] { local },
            System.Array.Empty<DecisionOutcome>(),
            null);

        Assert.Empty(resolved);
    }

    private static DecisionOutcome CreateClanOutcome(string clanId, Mock<IObjectManager> objectManager)
    {
        var clan = ObjectHelper.SkipConstructor<Clan>();
        string resolvedId = clanId;
        objectManager.Setup(manager => manager.TryGetId(clan, out resolvedId)).Returns(true);
        return new SettlementClaimantDecision.ClanAsDecisionOutcome(clan);
    }

    private static DecisionOutcome CreateBooleanOutcome(bool value)
    {
        return new DeclareWarDecision.DeclareWarDecisionOutcome(value, null, null);
    }
}
