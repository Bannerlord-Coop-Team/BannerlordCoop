#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopySteerCoopCommandTests
{
    [Fact]
    public void Validate_AcceptsKnownKnobsInRange()
    {
        Assert.Null(NavalCopySteerCoopCommand.Validate(new[] { "softzone=1", "tau=0.3", "lead=0.15" }));
    }

    // A refused knob leaves every knob as it was, so the arguments are checked before any applies.
    [Theory]
    [InlineData("lead=2", "lead (s) must be")]
    [InlineData("deadzone=1", "Unknown knob")]
    [InlineData("tau", "Expected key=value")]
    public void Validate_RefusesABadKnob(string knob, string expected)
    {
        Assert.StartsWith(expected, NavalCopySteerCoopCommand.Validate(new[] { "gain=2", knob }));
    }
}
#endif
