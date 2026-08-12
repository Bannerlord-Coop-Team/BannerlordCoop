using GameInterface.Services.MapEvents;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class BattleLootGrantRegistryTests
{
    [Fact]
    public void PartialClaim_UsesServerAwardForDiscardedRemainder()
    {
        var awarded = new ItemObject("awarded");
        var owned = new ItemObject("owned");
        var registry = new BattleLootGrantRegistry();
        registry.Stage(
            "controller",
            "hero",
            "party",
            "battle",
            new[] { Row(awarded, 3) });

        BattleLootClaimStatus status = registry.TryBeginClaim(
            "controller",
            "hero",
            "party",
            new[] { Row(awarded, 2) },
            new[] { Row(awarded, 1), Row(owned, 1), default },
            out BattleLootClaim claim,
            out string reason);

        Assert.Equal(BattleLootClaimStatus.Accepted, status);
        Assert.Null(reason);
        ItemRosterElement discarded = Assert.Single(claim.DiscardedItems);
        Assert.Same(awarded, discarded.EquipmentElement.Item);
        Assert.Equal(1, discarded.Amount);
        Assert.Empty(claim.ReturnedOwnedItems);
        Assert.True(registry.Consume(claim));
        Assert.Equal(
            BattleLootClaimStatus.NoGrant,
            registry.TryBeginClaim(
                "controller",
                "hero",
                "party",
                new[] { Row(awarded, 1) },
                new[] { Row(awarded, 2) },
                out _,
                out _));
    }

    [Fact]
    public void Claim_DoesNotTrustTemporaryClientLootRemainder()
    {
        var awarded = new ItemObject("awarded");
        var registry = new BattleLootGrantRegistry();
        registry.Stage(
            "controller",
            "hero",
            "party",
            "battle",
            new[] { Row(awarded, 3) });

        BattleLootClaimStatus status = registry.TryBeginClaim(
            "controller",
            "hero",
            "party",
            new[] { Row(awarded, 1) },
            new[] { Row(awarded, 1) },
            out _,
            out string reason);

        Assert.Equal(BattleLootClaimStatus.Accepted, status);
        Assert.Null(reason);
    }

    [Fact]
    public void Claim_CannotTakeMoreThanTheServerAwarded()
    {
        var awarded = new ItemObject("awarded");
        var registry = new BattleLootGrantRegistry();
        registry.Stage(
            "controller",
            "hero",
            "party",
            "battle",
            new[] { Row(awarded, 2) });

        BattleLootClaimStatus status = registry.TryBeginClaim(
            "controller",
            "hero",
            "party",
            new[] { Row(awarded, 3) },
            Enumerable.Empty<ItemRosterElement>(),
            out _,
            out string reason);

        Assert.Equal(BattleLootClaimStatus.Rejected, status);
        Assert.Contains("not present", reason);
    }

    [Fact]
    public void ConsumedGrant_CannotBeRestagedForTheSameMapEvent()
    {
        var awarded = new ItemObject("awarded");
        var registry = new BattleLootGrantRegistry();
        registry.Stage("controller", "hero", "party", "battle", new[] { Row(awarded, 1) });
        Assert.Equal(
            BattleLootClaimStatus.Accepted,
            registry.TryBeginClaim(
                "controller", "hero", "party",
                new[] { Row(awarded, 1) },
                Enumerable.Empty<ItemRosterElement>(),
                out BattleLootClaim claim,
                out _));
        Assert.True(registry.Consume(claim));

        registry.Stage("controller", "hero", "party", "battle", new[] { Row(awarded, 1) });

        Assert.Equal(
            BattleLootClaimStatus.NoGrant,
            registry.TryBeginClaim(
                "controller", "hero", "party",
                new[] { Row(awarded, 1) },
                Enumerable.Empty<ItemRosterElement>(),
                out _,
                out _));
    }

    [Fact]
    public void ForfeitedGrant_CannotBeRestagedAndWrongIdentityCannotClaim()
    {
        var awarded = new ItemObject("awarded");
        var registry = new BattleLootGrantRegistry();
        registry.Stage("controller", "hero", "party", "battle", new[] { Row(awarded, 1) });

        Assert.Equal(
            BattleLootClaimStatus.NoGrant,
            registry.TryBeginClaim(
                "controller", "other-hero", "party",
                new[] { Row(awarded, 1) },
                Enumerable.Empty<ItemRosterElement>(),
                out _,
                out _));

        registry.Forfeit("controller");
        registry.Stage("controller", "hero", "party", "battle", new[] { Row(awarded, 1) });

        Assert.Equal(
            BattleLootClaimStatus.NoGrant,
            registry.TryBeginClaim(
                "controller", "hero", "party",
                new[] { Row(awarded, 1) },
                Enumerable.Empty<ItemRosterElement>(),
                out _,
                out _));
    }

    [Fact]
    public void EmptyAward_DoesNotCreateAClaimableGrant()
    {
        var registry = new BattleLootGrantRegistry();
        registry.Stage(
            "controller", "hero", "party", "battle",
            new[] { default(ItemRosterElement) });

        Assert.Equal(
            BattleLootClaimStatus.NoGrant,
            registry.TryBeginClaim(
                "controller", "hero", "party",
                Enumerable.Empty<ItemRosterElement>(),
                Enumerable.Empty<ItemRosterElement>(),
                out _,
                out _));
    }

    [Fact]
    public void SameEventEmptyReplay_DoesNotDestroyPendingGrant()
    {
        var awarded = new ItemObject("awarded");
        var registry = new BattleLootGrantRegistry();
        registry.Stage("controller", "hero", "party", "battle", new[] { Row(awarded, 1) });

        registry.Stage(
            "controller", "hero", "party", "battle",
            new[] { default(ItemRosterElement) });

        Assert.Equal(
            BattleLootClaimStatus.Accepted,
            registry.TryBeginClaim(
                "controller", "hero", "party",
                new[] { Row(awarded, 1) },
                Enumerable.Empty<ItemRosterElement>(),
                out _,
                out _));
    }

    private static ItemRosterElement Row(ItemObject item, int amount) =>
        new(new EquipmentElement(item), amount);
}
