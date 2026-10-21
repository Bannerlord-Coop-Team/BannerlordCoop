#if DEBUG
using Missions.Naval;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalImmortalCoopCommandTests
{
    [Theory]
    [InlineData("on", Agent.MortalityState.Immortal)]
    [InlineData(" ON ", Agent.MortalityState.Immortal)]
    [InlineData("off", Agent.MortalityState.Mortal)]
    public void TryParseState_MapsOnToImmortalAndOffToMortal(string value, Agent.MortalityState expected)
    {
        Assert.True(NavalImmortalCoopCommand.TryParseState(value, out var state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invulnerable")]
    [InlineData(null)]
    public void TryParseState_RefusesAnythingElse(string? value)
    {
        Assert.False(NavalImmortalCoopCommand.TryParseState(value!, out _));
    }
}
#endif
