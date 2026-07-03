using Coop.Core.Common.Session;
using Xunit;

namespace Coop.Tests.Session;

public class ServerLaunchArgumentsTests
{
    [Fact]
    public void QuoteArgument_LeavesPlainArgumentAlone()
    {
        Assert.Equal("/server", ServerLaunchArguments.QuoteArgument("/server"));
        Assert.Equal("_MODULES_*Native*Coop*_MODULES_", ServerLaunchArguments.QuoteArgument("_MODULES_*Native*Coop*_MODULES_"));
    }

    [Fact]
    public void QuoteArgument_QuotesSpaces()
    {
        Assert.Equal("\"My Save\"", ServerLaunchArguments.QuoteArgument("My Save"));
        Assert.Equal("\"\"", ServerLaunchArguments.QuoteArgument(""));
    }

    [Fact]
    public void QuoteArgument_EscapesEmbeddedQuotes()
    {
        Assert.Equal("\"say \\\"hi\\\"\"", ServerLaunchArguments.QuoteArgument("say \"hi\""));
    }

    [Fact]
    public void QuoteArgument_DoublesTrailingBackslashes()
    {
        Assert.Equal("\"a path\\\\\"", ServerLaunchArguments.QuoteArgument("a path\\"));
    }

    [Fact]
    public void QuoteArgument_DoublesBackslashesBeforeEmbeddedQuote()
    {
        Assert.Equal("\"a\\\\\\\"b\"", ServerLaunchArguments.QuoteArgument("a\\\"b"));
    }

    [Fact]
    public void TryParse_FindsSaveNameAndOwner()
    {
        var args = new[] { "Bannerlord.exe", "/server", "/coopsave", "My Save", "/coopowner", "1234" };

        Assert.True(ServerLaunchArguments.TryParse(args, out var saveName, out var ownerProcessId));
        Assert.Equal("My Save", saveName);
        Assert.Equal(1234, ownerProcessId);
    }

    [Fact]
    public void TryParse_IsCaseInsensitive()
    {
        var args = new[] { "/COOPSAVE", "save1", "/CoopOwner", "42" };

        Assert.True(ServerLaunchArguments.TryParse(args, out var saveName, out var ownerProcessId));
        Assert.Equal("save1", saveName);
        Assert.Equal(42, ownerProcessId);
    }

    [Fact]
    public void TryParse_FailsWithoutSaveName()
    {
        Assert.False(ServerLaunchArguments.TryParse(new[] { "/server", "/coopowner", "1234" }, out _, out _));
        Assert.False(ServerLaunchArguments.TryParse(new[] { "/coopsave" }, out _, out _));
        Assert.False(ServerLaunchArguments.TryParse(System.Array.Empty<string>(), out _, out _));
    }

    [Fact]
    public void TryParse_ToleratesBadOwnerPid()
    {
        var args = new[] { "/coopsave", "save1", "/coopowner", "notanumber" };

        Assert.True(ServerLaunchArguments.TryParse(args, out var saveName, out var ownerProcessId));
        Assert.Equal("save1", saveName);
        Assert.Equal(0, ownerProcessId);
    }

    [Fact]
    public void BuildChildArguments_ForcesServerAndAppendsManagedArguments()
    {
        var current = new[] { "/singleplayer", "_MODULES_*Native*Coop*_MODULES_" };

        var built = ServerLaunchArguments.BuildChildArguments(current, "My Save", 1234);

        Assert.Equal("/singleplayer _MODULES_*Native*Coop*_MODULES_ /server /coopsave \"My Save\" /coopowner 1234", built);
    }

    [Fact]
    public void BuildChildArguments_StripsRoleAndSteamJoinArguments()
    {
        var current = new[]
        {
            "/singleplayer", "/client", "/autoconnect", "+connect_lobby", "109775240976525422",
            "/coopsave", "old save", "/coopowner", "99", "_MODULES_*Native*Coop*_MODULES_",
        };

        var built = ServerLaunchArguments.BuildChildArguments(current, "save1", 7);

        Assert.Equal("/singleplayer _MODULES_*Native*Coop*_MODULES_ /server /coopsave save1 /coopowner 7", built);
    }

    [Fact]
    public void BuildChildArguments_DoesNotDuplicateServerFlag()
    {
        var built = ServerLaunchArguments.BuildChildArguments(new[] { "/server" }, "save1", 7);

        Assert.Equal("/server /coopsave save1 /coopowner 7", built);
    }

    [Fact]
    public void BuildChildArguments_KeepsPlatformId()
    {
        var current = new[] { "/singleplayer", "/platformId", "testclient1" };

        var built = ServerLaunchArguments.BuildChildArguments(current, "save1", 7);

        Assert.Equal("/singleplayer /platformId testclient1 /server /coopsave save1 /coopowner 7", built);
    }
}
