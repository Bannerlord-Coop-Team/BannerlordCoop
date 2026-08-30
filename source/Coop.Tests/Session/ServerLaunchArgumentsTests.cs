using Common.Network;
using Common.Network.Session;
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
    public void TryParse_FindsPasswordWithoutChangingSaveResult()
    {
        var args = new[]
        {
            "Bannerlord.exe", "/server", "/coopsave", "My Save", "/coopowner", "1234",
            "/cooppassword", "Secret words",
        };

        Assert.True(ServerLaunchArguments.TryParse(
            args, out var saveName, out var ownerProcessId, out var password));
        Assert.Equal("My Save", saveName);
        Assert.Equal(1234, ownerProcessId);
        Assert.Equal("Secret words", password);
    }

    [Theory]
    [InlineData("public", ServerVisibility.Public)]
    [InlineData("FRIENDS_ONLY", ServerVisibility.FriendsOnly)]
    [InlineData("friends", ServerVisibility.FriendsOnly)]
    [InlineData("friendsonly", ServerVisibility.FriendsOnly)]
    [InlineData("none", ServerVisibility.None)]
    public void TryParse_FindsVisibilityCaseInsensitively(string value, ServerVisibility expected)
    {
        var args = new[]
        {
            ServerLaunchArguments.SaveArgument,
            "Campaign",
            ServerLaunchArguments.VisibilityArgument,
            value,
        };

        Assert.True(ServerLaunchArguments.TryParse(
            args, out _, out _, out _, out var visibility));
        Assert.Equal(expected, visibility);
    }

    [Fact]
    public void TryParse_DefaultsMissingVisibilityToPublicForLegacyLaunches()
    {
        var args = new[] { ServerLaunchArguments.SaveArgument, "Campaign" };

        Assert.True(ServerLaunchArguments.TryParse(
            args, out _, out _, out _, out var visibility));
        Assert.Equal(ServerVisibility.Public, visibility);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public void TryParse_RejectsInvalidExplicitVisibility(string value)
    {
        var args = new[]
        {
            ServerLaunchArguments.SaveArgument,
            "Campaign",
            ServerLaunchArguments.VisibilityArgument,
            value,
        };

        Assert.False(ServerLaunchArguments.TryParse(
            args, out _, out _, out _, out var visibility));
        Assert.Equal(ServerVisibility.None, visibility);
    }

    [Fact]
    public void TryParse_RejectsVisibilityWithoutValue()
    {
        var args = new[]
        {
            ServerLaunchArguments.SaveArgument,
            "Campaign",
            ServerLaunchArguments.VisibilityArgument,
        };

        Assert.False(ServerLaunchArguments.TryParse(
            args, out _, out _, out _, out var visibility));
        Assert.Equal(ServerVisibility.None, visibility);
    }

    [Fact]
    public void TryParse_ReturnsPasswordEvenWithoutAutoLoadSave()
    {
        var args = new[] { "/server", "/cooppassword", "Secret" };

        Assert.False(ServerLaunchArguments.TryParse(args, out _, out _, out var password));
        Assert.Equal("Secret", password);
    }

    [Fact]
    public void TryParse_RejectsAnOverlongPassword()
    {
        var args = new[]
        {
            ServerLaunchArguments.SaveArgument,
            "Campaign",
            ServerLaunchArguments.PasswordArgument,
            new string('x', ConnectionPassword.MaxLength + 1),
        };

        Assert.False(ServerLaunchArguments.TryParse(args, out _, out _, out var password));
        Assert.Equal(string.Empty, password);
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
    public void BuildModuleList_FormatsEngineToken()
    {
        Assert.Equal("_MODULES_*Native*SandBoxCore*SandBox*StoryMode*Coop*_MODULES_",
            ServerLaunchArguments.BuildModuleList(new[] { "Native", "SandBoxCore", "SandBox", "StoryMode", "Coop" }));
    }

    [Fact]
    public void BuildManagedServerArguments_MatchesTheStartServerShape()
    {
        var built = ServerLaunchArguments.BuildManagedServerArguments(
            new[] { "Native", "SandBoxCore", "SandBox", "StoryMode", "Coop" }, "MP", 1234);

        Assert.Equal("/singleplayer /server _MODULES_*Native*SandBoxCore*SandBox*StoryMode*Coop*_MODULES_ /coopsave MP /coopowner 1234 /coopvisibility public", built);
    }

    [Fact]
    public void BuildManagedServerArguments_QuotesSaveNameWithSpaces()
    {
        var built = ServerLaunchArguments.BuildManagedServerArguments(new[] { "Native", "Coop" }, "My Save", 42);

        Assert.Equal("/singleplayer /server _MODULES_*Native*Coop*_MODULES_ /coopsave \"My Save\" /coopowner 42 /coopvisibility public", built);
    }

    [Fact]
    public void BuildManagedServerArguments_AppendsQuotedPasswordWhenProtected()
    {
        var built = ServerLaunchArguments.BuildManagedServerArguments(
            new[] { "Native", "Coop" }, "My Save", 42, "Secret words");

        Assert.Equal("/singleplayer /server _MODULES_*Native*Coop*_MODULES_ /coopsave \"My Save\" /coopowner 42 /coopvisibility public /cooppassword \"Secret words\"", built);
    }

    [Theory]
    [InlineData(ServerVisibility.Public, "public")]
    [InlineData(ServerVisibility.FriendsOnly, "friends_only")]
    [InlineData(ServerVisibility.None, "none")]
    public void BuildManagedServerArguments_AppendsVisibility(ServerVisibility visibility, string expected)
    {
        var built = ServerLaunchArguments.BuildManagedServerArguments(
            new[] { "Native", "Coop" }, "My Save", 42, string.Empty, visibility);

        Assert.Contains($"{ServerLaunchArguments.VisibilityArgument} {expected}", built);
    }

    [Fact]
    public void BuildManagedServerArguments_OmitsPasswordArgumentWhenUnprotected()
    {
        var built = ServerLaunchArguments.BuildManagedServerArguments(
            new[] { "Native", "Coop" }, "My Save", 42, string.Empty);

        Assert.DoesNotContain(ServerLaunchArguments.PasswordArgument, built);
    }

    [Fact]
    public void BuildDedicatedServerArguments_CarriesOnlyTheCoopSessionTokens()
    {
        var built = ServerLaunchArguments.BuildDedicatedServerArguments(
            "My Save", 42, "Secret words", ServerVisibility.FriendsOnly);

        Assert.Equal("/coopsave \"My Save\" /coopowner 42 /coopvisibility friends_only /cooppassword \"Secret words\"", built);
    }

    [Fact]
    public void BuildDedicatedServerArguments_OmitsEngineTokens()
    {
        var built = ServerLaunchArguments.BuildDedicatedServerArguments(
            "MP", 42, string.Empty, ServerVisibility.Public);

        Assert.DoesNotContain("/server", built);
        Assert.DoesNotContain("/singleplayer", built);
        Assert.DoesNotContain("_MODULES_", built);
    }

    [Fact]
    public void BuildDedicatedServerArguments_OmitsPasswordArgumentWhenUnprotected()
    {
        var built = ServerLaunchArguments.BuildDedicatedServerArguments(
            "MP", 42, string.Empty, ServerVisibility.Public);

        Assert.Equal("/coopsave MP /coopowner 42 /coopvisibility public", built);
    }

    [Fact]
    public void BuildDedicatedServerArguments_RoundTripsThroughTryParse()
    {
        var built = ServerLaunchArguments.BuildDedicatedServerArguments(
            "My Save", 1234, "Secret words", ServerVisibility.None);
        var args = SplitLikeWindows(built);

        Assert.True(ServerLaunchArguments.TryParse(
            args, out var saveName, out var ownerProcessId, out var password, out var visibility));
        Assert.Equal("My Save", saveName);
        Assert.Equal(1234, ownerProcessId);
        Assert.Equal("Secret words", password);
        Assert.Equal(ServerVisibility.None, visibility);
    }

    [Fact]
    public void BuildDedicatedServerArguments_RejectsMissingSaveAndUndefinedVisibility()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            ServerLaunchArguments.BuildDedicatedServerArguments(null, 42, "", ServerVisibility.Public));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            ServerLaunchArguments.BuildDedicatedServerArguments("MP", 42, "", (ServerVisibility)99));
    }

    /// <summary>Minimal Windows-rule splitter for round-trip tests (quotes only; the
    /// builder never emits embedded quotes for these inputs).</summary>
    private static string[] SplitLikeWindows(string commandLine)
    {
        var tokens = new System.Collections.Generic.List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (var c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens.ToArray();
    }
}
