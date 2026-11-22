using Common;
using Common.Commands;
using Common.Util;
using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Tests.Bootstrap;
using GameInterface.Utils.Commands;
using Moq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class KingdomDebugCommandTests
{
    [Fact]
    public void ForceAlly_WithEliminatedKingdom_ReturnsErrorWithoutFormingAlliance()
    {
        var living = ObjectHelper.SkipConstructor<Kingdom>();
        var eliminated = ObjectHelper.SkipConstructor<Kingdom>();
        eliminated._isEliminated = true;
        var resolver = BuildResolver(("alive", living), ("dead", eliminated));

        var result = InvokeAsServer(
            () => new KingdomDebugCommand.KingdomForceAllyCoopCommand(resolver).ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { "alive", "dead" })));

        Assert.False(result.Succeeded);
        Assert.Contains("has been eliminated", result.Output);
    }

    [Fact]
    public void ForceTradeAgreement_WithEliminatedKingdom_ReturnsErrorWithoutFormingAgreement()
    {
        var living = ObjectHelper.SkipConstructor<Kingdom>();
        var eliminated = ObjectHelper.SkipConstructor<Kingdom>();
        eliminated._isEliminated = true;
        var resolver = BuildResolver(("alive", living), ("dead", eliminated));

        var result = InvokeAsServer(
            () => new KingdomDebugCommand.KingdomForceTradeAgreementCoopCommand(resolver).ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { "alive", "dead" })));

        Assert.False(result.Succeeded);
        Assert.Contains("has been eliminated", result.Output);
    }

    [Fact]
    public void ForceTradeAgreement_WithExpiredAgreement_RenewsInsteadOfReportingExisting()
    {
        GameBootStrap.Initialize();
        var behavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
        Assert.NotNull(behavior);

        var kingdom1 = ObjectHelper.SkipConstructor<Kingdom>();
        var kingdom2 = ObjectHelper.SkipConstructor<Kingdom>();

        // Seed a lapsed agreement that the lazy cleanup has not yet removed.
        // CampaignTime.Now is patched to Zero in tests while IsPast still compares against the real
        // map-time ticks, so an EndTime of "now" reads as already expired.
        behavior._tradeAgreements.RemoveAll(t =>
            (t.Kingdom1 == kingdom1 && t.Kingdom2 == kingdom2) ||
            (t.Kingdom1 == kingdom2 && t.Kingdom2 == kingdom1));
        var expired = new TradeAgreementsCampaignBehavior.TradeAgreement(kingdom1, kingdom2, CampaignTime.Now);
        behavior._tradeAgreements.Add(expired);
        Assert.True(expired.EndTime.IsPast);

        var resolver = BuildResolver(("k1", kingdom1), ("k2", kingdom2));
        var result = InvokeAsServer(
            () => new KingdomDebugCommand.KingdomForceTradeAgreementCoopCommand(resolver).ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { "k1", "k2" })));

        // The raw-list guard would have reported the stale entry and bailed; HasTradeAgreement drops it.
        Assert.True(result.Succeeded);
        Assert.StartsWith("Forced trade agreement", result.Output);
        Assert.True(behavior.TryGetTradeAgreement(kingdom1, kingdom2, out var index));
        Assert.True(behavior._tradeAgreements[index].EndTime.NumTicks > 0);
    }

    // Builds a resolver that maps each test id to its kingdom and reports a miss for anything else.
    private static ICommandObjectResolver BuildResolver(params (string Id, Kingdom Kingdom)[] kingdoms)
    {
        var mock = new Mock<ICommandObjectResolver>();
        foreach (var (id, kingdom) in kingdoms)
        {
            var resolved = kingdom;
            string noError = null;
            mock.Setup(resolver => resolver.TryResolveKingdom(id, out resolved, out noError)).Returns(true);
        }
        return mock.Object;
    }

    // Runs the command with the server role set, restoring the previous role afterwards.
    private static CoopCommandResult InvokeAsServer(Func<CoopCommandResult> invoke)
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            return invoke();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
