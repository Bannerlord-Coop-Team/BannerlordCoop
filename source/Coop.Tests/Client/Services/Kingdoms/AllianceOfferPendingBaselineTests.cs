using Coop.Core.Client.Services.Kingdoms;
using Coop.Core.Server.Services.Kingdoms;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using System;
using Xunit;

namespace Coop.Tests.Server.Services.Kingdoms;

public class AllianceOfferPendingBaselineTests : IDisposable
{
    private static readonly (string RequestingKingdomId, string TargetKingdomId)[] Empty = Array.Empty<(string, string)>();

    public AllianceOfferPendingBaselineTests()
    {
        AllianceOfferPendingRegistry.RestoreAll(Empty);
    }

    public void Dispose()
    {
        AllianceOfferPendingRegistry.RestoreAll(Empty);
    }

    [Fact]
    public void Capture_ReturnsAllPendingOffers()
    {
        AllianceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        AllianceOfferPendingRegistry.Set("kingdom_c", "kingdom_d", true);
        var capturer = new AllianceOfferPendingCapturer();

        PendingAllianceOfferBaseline[] captured = capturer.Capture();

        Assert.Equal(2, captured.Length);
        Assert.Contains(captured, o => o.RequestingKingdomId == "kingdom_a" && o.TargetKingdomId == "kingdom_b");
        Assert.Contains(captured, o => o.RequestingKingdomId == "kingdom_c" && o.TargetKingdomId == "kingdom_d");
    }

    [Fact]
    public void Capture_ExcludesOffersThatWereWithdrawn()
    {
        AllianceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        AllianceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", false);
        var capturer = new AllianceOfferPendingCapturer();

        PendingAllianceOfferBaseline[] captured = capturer.Capture();

        Assert.Empty(captured);
    }

    [Fact]
    public void Capture_EmptyRegistry_ReturnsEmptyArray()
    {
        var capturer = new AllianceOfferPendingCapturer();

        PendingAllianceOfferBaseline[] captured = capturer.Capture();

        Assert.Empty(captured);
    }

    [Fact]
    public void Apply_RestoresPendingStateForEachOffer()
    {
        var applier = new AllianceOfferPendingApplier();

        applier.Apply(new[]
        {
            new PendingAllianceOfferBaseline("kingdom_a", "kingdom_b"),
            new PendingAllianceOfferBaseline("kingdom_c", "kingdom_d"),
        });

        Assert.True(AllianceOfferPendingRegistry.IsPending("kingdom_a", "kingdom_b"));
        Assert.True(AllianceOfferPendingRegistry.IsPending("kingdom_c", "kingdom_d"));
    }

    [Fact]
    public void Apply_ClearsStaleEntriesNotInTheNewBaseline()
    {
        AllianceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        var applier = new AllianceOfferPendingApplier();

        applier.Apply(new[]
        {
            new PendingAllianceOfferBaseline("kingdom_c", "kingdom_d"),
        });

        Assert.False(AllianceOfferPendingRegistry.IsPending("kingdom_a", "kingdom_b"));
        Assert.True(AllianceOfferPendingRegistry.IsPending("kingdom_c", "kingdom_d"));
    }

    [Fact]
    public void Apply_NullOffers_ClearsRegistryWithoutThrowing()
    {
        AllianceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        var applier = new AllianceOfferPendingApplier();

        applier.Apply(null);

        Assert.False(AllianceOfferPendingRegistry.IsPending("kingdom_a", "kingdom_b"));
    }

    [Fact]
    public void CaptureThenApply_RoundTripsPendingOffers()
    {
        AllianceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        var captured = new AllianceOfferPendingCapturer().Capture();
        AllianceOfferPendingRegistry.RestoreAll(Empty);

        new AllianceOfferPendingApplier().Apply(captured);

        Assert.True(AllianceOfferPendingRegistry.IsPending("kingdom_a", "kingdom_b"));
    }
}