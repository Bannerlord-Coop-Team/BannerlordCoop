#if DEBUG
using Missions.Naval;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalReswapCoopCommandTests
{
    private static readonly Guid First = Guid.Parse("1d93665a-64a9-409b-8736-f77261b57865");
    private static readonly Guid Second = Guid.Parse("1e78828e-627f-41e5-982f-76df24366941");

    [Theory]
    [InlineData("1d93")]
    [InlineData("1D93665A")]
    [InlineData(" 1d93665a-64a9 ")]
    public void TryResolveShipId_ResolvesTheOneIdWithThePrefix(string prefix)
    {
        Assert.Null(NavalReswapCoopCommand.TryResolveShipId(new[] { First, Second }, prefix, out var shipId));

        Assert.Equal(First, shipId);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("ff")]
    [InlineData("")]
    [InlineData(" ")]
    public void TryResolveShipId_RejectsAnAmbiguousMissingOrEmptyPrefix(string prefix)
    {
        Assert.NotNull(NavalReswapCoopCommand.TryResolveShipId(new[] { First, Second }, prefix, out var shipId));

        Assert.Equal(Guid.Empty, shipId);
    }
}
#endif
