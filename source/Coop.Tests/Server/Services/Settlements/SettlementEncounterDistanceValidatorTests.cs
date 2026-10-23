using Coop.Core.Server.Services.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Server.Services.Settlements;

/// <summary>Verifies land and port settlement interaction coordinates remain distinct.</summary>
public class SettlementEncounterDistanceValidatorTests
{
    [Fact]
    public void LandEntry_AtSettlementCenterFarFromGate_IsAllowed()
    {
        Assert.True(SettlementEncounterDistanceValidator.IsWithinInteractionDistance(
            Position(50f, 60f),
            Position(50f, 60f),
            Position(20f, 30f),
            Position(90f, 100f),
            usePort: false,
            maximumDistance: 6f));
    }

    [Fact]
    public void PortEntry_AtSettlementCenterFarFromPort_IsRejected()
    {
        Assert.False(SettlementEncounterDistanceValidator.IsWithinInteractionDistance(
            Position(50f, 60f),
            Position(50f, 60f),
            Position(20f, 30f),
            Position(90f, 100f),
            usePort: true,
            maximumDistance: 6f));
    }

    [Fact]
    public void LandEntry_FarFromCenterAndGate_IsRejected()
    {
        Assert.False(SettlementEncounterDistanceValidator.IsWithinInteractionDistance(
            Position(900f, 900f),
            Position(50f, 60f),
            Position(20f, 30f),
            Position(90f, 100f),
            usePort: false,
            maximumDistance: 6f));
    }

    [Theory]
    [InlineData(40.5f, 50.5f, true)]
    [InlineData(41.4f, 50.5f, true)]
    [InlineData(42f, 50.5f, false)]
    [InlineData(20f, 30f, false)]
    public void SiegeCamp_ContainsOnlyTheRolledSlotArea(float x, float y, bool expected)
    {
        var frame = MatrixFrame.Identity;
        frame.origin = new Vec3(40f, 50f, 0f);

        Assert.Equal(expected, SettlementEncounterDistanceValidator.IsInSiegeCamp(
            Position(x, y),
            new[] { frame },
            slack: 0.5f));
    }

    [Fact]
    public void SiegeCamp_FollowsARotatedSlot()
    {
        var frame = MatrixFrame.Identity;
        frame.origin = new Vec3(40f, 50f, 0f);
        frame.rotation.s = new Vec3(0f, 2f, 0f);
        frame.rotation.f = new Vec3(-2f, 0f, 0f);

        Assert.True(SettlementEncounterDistanceValidator.IsInSiegeCamp(
            Position(38.5f, 51.5f), new[] { frame }, slack: 0.5f));
        Assert.False(SettlementEncounterDistanceValidator.IsInSiegeCamp(
            Position(41.5f, 51.5f), new[] { frame }, slack: 0.5f));
    }

    [Fact]
    public void SiegeCamp_WithoutFrames_IsRejected()
    {
        Assert.False(SettlementEncounterDistanceValidator.IsInSiegeCamp(
            Position(40f, 50f), null, slack: 0.5f));
        Assert.False(SettlementEncounterDistanceValidator.IsInSiegeCamp(
            Position(40f, 50f), System.Array.Empty<MatrixFrame>(), slack: 0.5f));
    }

    private static CampaignVec2 Position(float x, float y)
    {
        return new CampaignVec2(new Vec2(x, y), true);
    }
}
