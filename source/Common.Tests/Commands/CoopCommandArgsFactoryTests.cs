using Common.Commands;

namespace Common.Tests.Commands;

public class CoopCommandArgsFactoryTests
{
    private readonly CoopCommandArgsFactory factory = new CoopCommandArgsFactory();

    [Fact]
    public void FromValues_PreservesStructuredArgumentBoundaries()
    {
        ICoopCommandArgs args = factory.FromValues(new[]
        {
            "argument with spaces",
            "quoted \"value\"",
        });

        Assert.Equal(2, args.Count);
        Assert.Equal("argument with spaces", args[0]);
        Assert.Equal("quoted \"value\"", args[1]);
    }

    [Fact]
    public void TryFromConsoleTokens_MergesDoubleQuotedTokens()
    {
        bool parsed = factory.TryFromConsoleTokens(
            new[] { "\"argument", "with", "spaces\"", "tail" },
            out ICoopCommandArgs args,
            out string error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(new[] { "argument with spaces", "tail" }, args);
    }

    [Fact]
    public void TryFromConsoleTokens_PreservesEscapedQuotesAndBackslashes()
    {
        bool parsed = factory.TryFromConsoleTokens(
            new[] { "\"quoted", "\\\"value\\\"", "at", "c:\\\\temp\"" },
            out ICoopCommandArgs args,
            out string error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(new[] { "quoted \"value\" at c:\\temp" }, args);
    }

    [Fact]
    public void TryFromConsoleTokens_PreservesEmptyQuotedArgument()
    {
        bool parsed = factory.TryFromConsoleTokens(
            new[] { "\"\"" },
            out ICoopCommandArgs args,
            out string error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Single(args);
        Assert.Equal(string.Empty, args[0]);
    }

    [Fact]
    public void TryFromConsoleTokens_RejectsUnterminatedQuote()
    {
        bool parsed = factory.TryFromConsoleTokens(
            new[] { "\"argument", "with", "spaces" },
            out ICoopCommandArgs args,
            out string error);

        Assert.False(parsed);
        Assert.Null(args);
        Assert.Equal("Command arguments contain an unterminated double quote.", error);
    }
}
