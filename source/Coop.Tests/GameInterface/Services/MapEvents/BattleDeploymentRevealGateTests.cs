using System;
using GameInterface.Services.MapEvents;
using Moq;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

/// <summary>
/// Unit tests for <see cref="BattleDeploymentRevealGate"/> — the "hidden everywhere until deployed" rule (#4):
/// the local player's own-party troops are withheld from peers until that player commits deployment, while the
/// host's NPC/AI (not own-party) replicates immediately so it shows frozen during deployment (#1). Covers the
/// withhold decision in every state, commit idempotency, the post-commit transition, and reinforcements.
/// </summary>
public class BattleDeploymentRevealGateTests
{
    private readonly Mock<IBattleDeploymentRevealSink> sink = new(MockBehavior.Strict);
    private readonly BattleDeploymentRevealGate sut;

    public BattleDeploymentRevealGateTests()
    {
        sut = new BattleDeploymentRevealGate(sink.Object);
    }

    [Fact]
    public void NullSink_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BattleDeploymentRevealGate(null));
    }

    [Fact]
    public void BeforeCommit_IsNotCommitted()
    {
        Assert.False(sut.IsCommitted);
    }

    [Fact]
    public void OwnPartyTroop_DuringDeployment_IsWithheld()
    {
        // Requirement #4: our own troops are not replicated to peers while we are still placing them.
        Assert.True(sut.ShouldWithhold(isOwnPartyTroop: true));
        sink.Verify(s => s.RevealOwnTroopsAtDeployedPositions(), Times.Never);
    }

    [Fact]
    public void NpcTroop_DuringDeployment_IsNotWithheld()
    {
        // Requirement #1: the host's NPC/AI must show up frozen on every client during deployment.
        Assert.False(sut.ShouldWithhold(isOwnPartyTroop: false));
        sink.Verify(s => s.RevealOwnTroopsAtDeployedPositions(), Times.Never);
    }

    [Fact]
    public void ShouldWithhold_IsSideEffectFree()
    {
        // Querying never commits or reveals, regardless of how many times or with which argument.
        sut.ShouldWithhold(true);
        sut.ShouldWithhold(false);
        sut.ShouldWithhold(true);

        Assert.False(sut.IsCommitted);
        sink.Verify(s => s.RevealOwnTroopsAtDeployedPositions(), Times.Never);
    }

    [Fact]
    public void Commit_RevealsOwnTroopsOnce_AndMarksCommitted()
    {
        sink.Setup(s => s.RevealOwnTroopsAtDeployedPositions());

        sut.OnDeploymentCommitted();

        Assert.True(sut.IsCommitted);
        sink.Verify(s => s.RevealOwnTroopsAtDeployedPositions(), Times.Once);
    }

    [Fact]
    public void DoubleCommit_IsIdempotent_RevealsOnce()
    {
        // A second commit (e.g. a re-entrant or duplicate OnDeploymentFinished) must not re-broadcast the troops.
        sink.Setup(s => s.RevealOwnTroopsAtDeployedPositions());

        sut.OnDeploymentCommitted();
        sut.OnDeploymentCommitted();

        Assert.True(sut.IsCommitted);
        sink.Verify(s => s.RevealOwnTroopsAtDeployedPositions(), Times.Once);
    }

    [Fact]
    public void OwnPartyReinforcement_AfterCommit_IsNotWithheld()
    {
        sink.Setup(s => s.RevealOwnTroopsAtDeployedPositions());
        sut.OnDeploymentCommitted();

        // A reinforcement of our own party spawning after we deployed replicates immediately, like any owned spawn.
        Assert.False(sut.ShouldWithhold(isOwnPartyTroop: true));
    }

    [Fact]
    public void NpcTroop_AfterCommit_IsNotWithheld()
    {
        sink.Setup(s => s.RevealOwnTroopsAtDeployedPositions());
        sut.OnDeploymentCommitted();

        Assert.False(sut.ShouldWithhold(isOwnPartyTroop: false));
    }
}
