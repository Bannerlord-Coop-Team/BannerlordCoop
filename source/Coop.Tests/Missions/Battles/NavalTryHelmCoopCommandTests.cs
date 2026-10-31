#if COOP_NAVAL
using Missions.Naval;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalTryHelmCoopCommandTests
{
    [Theory]
    [InlineData("72da8e4a", "72da8e4a", false)]
    [InlineData("72da8e4a force", "72da8e4a", true)]
    [InlineData("72da8e4a FORCE", "72da8e4a", true)]
    public void TryParse_ReadsThePrefixAndForce(string argLine, string prefix, bool force)
    {
        Assert.True(NavalTryHelmCoopCommand.TryParse(Args(argLine), out var parsedPrefix, out var parsedForce, out var error), error);

        Assert.Equal(prefix, parsedPrefix);
        Assert.Equal(force, parsedForce);
    }

    [Fact]
    public void TryParse_TrimsThePrefix()
    {
        Assert.True(NavalTryHelmCoopCommand.TryParse(new[] { " 72da " }, out var prefix, out _, out var error), error);
        Assert.Equal("72da", prefix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("72da8e4a now")]
    [InlineData("72da8e4a force extra")]
    public void TryParse_RejectsMalformedArguments(string argLine)
    {
        Assert.False(NavalTryHelmCoopCommand.TryParse(Args(argLine), out _, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryParse_RejectsABlankPrefix()
    {
        Assert.False(NavalTryHelmCoopCommand.TryParse(new[] { " " }, out _, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryParse_RejectsNoArguments()
    {
        Assert.False(NavalTryHelmCoopCommand.TryParse(null, out _, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    // Splits a console argument line the way the console does.
    private static string[] Args(string line) => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
}
#endif
