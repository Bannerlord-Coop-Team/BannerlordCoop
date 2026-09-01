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

    private static CampaignVec2 Position(float x, float y)
    {
        return new CampaignVec2(new Vec2(x, y), true);
    }
}
