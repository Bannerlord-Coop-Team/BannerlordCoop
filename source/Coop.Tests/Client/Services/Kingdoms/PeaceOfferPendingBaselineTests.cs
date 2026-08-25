using Coop.Core.Client.Services.Kingdoms;
using Coop.Core.Server.Services.Kingdoms;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using System;
using Xunit;

namespace Coop.Tests.Client.Services.Kingdoms;

public class PeaceOfferPendingBaselineTests : IDisposable
{
    private static readonly (string RequestingKingdomId, string TargetKingdomId)[] Empty = Array.Empty<(string, string)>();

    public PeaceOfferPendingBaselineTests()
    {
        PeaceOfferPendingRegistry.RestoreAll(Empty);
    }

    public void Dispose()
    {
        PeaceOfferPendingRegistry.RestoreAll(Empty);
    }

    [Fact]
    public void Capture_ReturnsAllPendingOffers()
    {
        PeaceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        PeaceOfferPendingRegistry.Set("kingdom_c", "kingdom_d", true);
        var capturer = new PeaceOfferPendingCapturer();

        PendingPeaceOfferBaseline[] captured = capturer.Capture();

        Assert.Equal(2, captured.Length);
        Assert.Contains(captured, o => o.RequestingKingdomId == "kingdom_a" && o.TargetKingdomId == "kingdom_b");
        Assert.Contains(captured, o => o.RequestingKingdomId == "kingdom_c" && o.TargetKingdomId == "kingdom_d");
    }

    [Fact]
    public void Capture_ExcludesOffersThatWereWithdrawn()
    {
        PeaceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        PeaceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", false);
        var capturer = new PeaceOfferPendingCapturer();

        PendingPeaceOfferBaseline[] captured = capturer.Capture();

        Assert.Empty(captured);
    }

    [Fact]
    public void Capture_EmptyRegistry_ReturnsEmptyArray()
    {
        var capturer = new PeaceOfferPendingCapturer();

        PendingPeaceOfferBaseline[] captured = capturer.Capture();

        Assert.Empty(captured);
    }

    [Fact]
    public void Apply_RestoresPendingStateForEachOffer()
    {
        var applier = new PeaceOfferPendingApplier();

        applier.Apply(new[]
        {
            new PendingPeaceOfferBaseline("kingdom_a", "kingdom_b"),
            new PendingPeaceOfferBaseline("kingdom_c", "kingdom_d"),
        });

        Assert.True(PeaceOfferPendingRegistry.IsPending("kingdom_a", "kingdom_b"));
        Assert.True(PeaceOfferPendingRegistry.IsPending("kingdom_c", "kingdom_d"));
    }

    [Fact]
    public void Apply_ClearsStaleEntriesNotInTheNewBaseline()
    {
        PeaceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        var applier = new PeaceOfferPendingApplier();

        applier.Apply(new[]
        {
            new PendingPeaceOfferBaseline("kingdom_c", "kingdom_d"),
        });

        Assert.False(PeaceOfferPendingRegistry.IsPending("kingdom_a", "kingdom_b"));
        Assert.True(PeaceOfferPendingRegistry.IsPending("kingdom_c", "kingdom_d"));
    }

    [Fact]
    public void Apply_NullOffers_ClearsRegistryWithoutThrowing()
    {
        PeaceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        var applier = new PeaceOfferPendingApplier();

        applier.Apply(null);

        Assert.False(PeaceOfferPendingRegistry.IsPending("kingdom_a", "kingdom_b"));
    }

    [Fact]
    public void CaptureThenApply_RoundTripsPendingOffers()
    {
        PeaceOfferPendingRegistry.Set("kingdom_a", "kingdom_b", true);
        var captured = new PeaceOfferPendingCapturer().Capture();
        PeaceOfferPendingRegistry.RestoreAll(Empty);

        new PeaceOfferPendingApplier().Apply(captured);

        Assert.True(PeaceOfferPendingRegistry.IsPending("kingdom_a", "kingdom_b"));
    }
}
