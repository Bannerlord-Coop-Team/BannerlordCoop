using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// BR-102 receiver policy for host-authority mesh messages: only a message stamped with a strictly
/// LOWER epoch than the receiver's stored assignment is stale. The accept cases are as load-bearing
/// as the drop case — rejecting an equal epoch would drop the live host's traffic, and rejecting a
/// HIGHER epoch would silence the newly promoted host until the receiver's assignment broadcast
/// lands (the documented self-healing convergence window works in both directions).
/// </summary>
public class HostEpochPolicyTests
{
    [Theory]
    [Trait("Requirement", "BR-102")]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(1, 2)]
    public void MessageStampedBelowTheLocalAssignment_IsStale(int messageEpoch, int localEpoch)
    {
        Assert.True(HostEpochPolicy.IsStale(messageEpoch, localEpoch));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void CurrentEpochMessage_IsNotStale()
    {
        Assert.False(HostEpochPolicy.IsStale(3, 3));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void MessageAheadOfTheReceiver_IsNotStale()
    {
        // The sender heard about the migration before this receiver did (the server broadcast races
        // the P2P mesh); dropping would silence the NEW host until the assignment lands here.
        Assert.False(HostEpochPolicy.IsStale(4, 3));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void UnstampedMessage_IsNotStale()
    {
        // Epoch 0 = the sender has no assignment yet (or predates the stamping); nothing to judge.
        Assert.False(HostEpochPolicy.IsStale(0, 3));
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void ReceiverWithoutAnAssignment_CannotJudgeStaleness()
    {
        // Local epoch 0 = we have not received the election result yet; accept and let the
        // assignment broadcast converge.
        Assert.False(HostEpochPolicy.IsStale(2, 0));
    }
}
