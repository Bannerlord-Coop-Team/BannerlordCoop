#if COOP_NAVAL && DEBUG
using Common.Commands;
using Missions.Naval;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks fault injection console input before native mission access.</summary>
[Collection("Mission.Current")]
public class NavalDeckRejectCoopCommandTests
{
    // Reject invalid counts and ambiguous short prefixes before any fault can be armed.
    [Theory]
    [InlineData("1234567", "1")]
    [InlineData("12345678", "0")]
    [InlineData("12345678", "10001")]
    [InlineData("12345678", "bad")]
    [InlineData("not-an-id", "1")]
    [InlineData("clear", "1")]
    public void InvalidArguments_ShowUsage(string id, string count)
    {
        var result = new NavalDeckRejectCoopCommand().ProcessCommand(Args(id, count));
        Assert.False(result.Succeeded);
        Assert.StartsWith("Usage:", result.Output);
    }

    // Both the one-shot default and a sustained observation budget are valid.
    [Theory]
    [InlineData("abcdef12", null, 1)]
    [InlineData("abcdef12", "10000", 10000)]
    [InlineData("ABCDEF12-1234-1234-1234-123456789ABC", "2", 2)]
    public void ValidSelection_ParsesCount(string id, string? value, int expected)
    {
        Assert.True(NavalDeckRejectCoopCommand.TryParse(value == null ? Args(id) : Args(id, value), out string prefix, out int count));
        Assert.Equal(id, prefix);
        Assert.Equal(expected, count);
    }

    // Clearing or arming cannot leak out of a mission into the next battle.
    [Theory]
    [InlineData("clear")]
    [InlineData("abcdef12")]
    public void OutsideMission_RefusesFault(string value)
    {
        var result = new NavalDeckRejectCoopCommand().ProcessCommand(Args(value));
        Assert.False(result.Succeeded);
        Assert.Equal("No active coop naval battle mission.", result.Output);
    }

    // Supplies console arguments without requiring the command dispatcher.
    private static ICoopCommandArgs Args(params string[] values)
    {
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(a => a.Count).Returns(values.Length);
        args.Setup(a => a[It.IsAny<int>()]).Returns((int index) => values[index]);
        return args.Object;
    }
}
#endif
