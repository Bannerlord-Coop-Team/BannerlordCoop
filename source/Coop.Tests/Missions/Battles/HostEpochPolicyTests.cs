using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// BR-102 receiver policy for host-authority mesh messages: a message stamped with an epoch strictly
/// LOWER than the receiver's stored assignment — or than the highest epoch it has already accepted — is
/// stale. The accept cases are as load-bearing as the drop case: rejecting an equal epoch would drop the
/// live host's traffic, and rejecting a HIGHER epoch would silence the newly promoted host until the
/// receiver's assignment broadcast lands (the documented self-healing convergence window works in both
/// directions). The accepted-epoch high-water mark then keeps a delayed lower-but-still-ahead message
/// from a superseded generation from being applied last.
/// </summary>
public class HostEpochPolicyTests
{
    private readonly HostEpochPolicy policy = new();

    [Theory]
    [Trait("Requirement", "BR-102")]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(1, 2)]
    public void MessageStampedBelowTheLocalAssignment_IsStale(int messageEpoch, int localEpoch)
    {
        Assert.True(policy.IsStale(messageEpoch, localEpoch));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void CurrentEpochMessage_IsNotStale()
    {
        Assert.False(policy.IsStale(3, 3));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void MessageAheadOfTheReceiver_IsNotStale()
    {
        // The sender heard about the migration before this receiver did (the server broadcast races
        // the P2P mesh); dropping would silence the NEW host until the assignment lands here.
        Assert.False(policy.IsStale(4, 3));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void UnstampedMessage_IsNotStale()
    {
        // Epoch 0 = the sender has no assignment yet (or predates the stamping); nothing to judge.
        Assert.False(policy.IsStale(0, 3));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void ReceiverWithoutAnAssignment_CannotJudgeStaleness()
    {
        // Local epoch 0 = we have not received the election result yet; accept and let the
        // assignment broadcast converge.
        Assert.False(policy.IsStale(2, 0));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void AcceptedHigherEpoch_MakesADelayedLowerEpochStale()
    {
        // The reviewer's exact scenario. A receiver still on epoch 1 accepts an epoch-3 message
        // (3 > 1, ahead-of-assignment)...
        Assert.False(policy.IsStale(messageEpoch: 3, localEpoch: 1));

        // ...then a delayed epoch-2 message from a superseded generation arrives. It is still ahead of
        // the stored assignment (2 > 1), so the per-message assignment check ALONE would accept it and
        // apply that older siege state last. The accepted-epoch watermark (raised to 3 above) drops it.
        // Pre-fix (no watermark) IsStale(2, 1) returns false and this assertion fails.
        Assert.True(policy.IsStale(messageEpoch: 2, localEpoch: 1));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void AcceptedEpochWatermark_IsIndependentPerPolicyInstance()
    {
        // One battle's policy accepts epoch 3, so its own delayed epoch-2 is stale...
        var first = new HostEpochPolicy();
        Assert.False(first.IsStale(messageEpoch: 3, localEpoch: 1));
        Assert.True(first.IsStale(messageEpoch: 2, localEpoch: 1));

        // ...but a second policy (a different battle) has its own watermark starting clean: the same
        // delayed epoch-2 message is NOT stale, proving the watermark does not leak across battles.
        var second = new HostEpochPolicy();
        Assert.False(second.IsStale(messageEpoch: 2, localEpoch: 1));
    }
}
