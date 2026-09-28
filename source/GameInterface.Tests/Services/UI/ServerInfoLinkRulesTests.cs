using GameInterface.Services.UI.ServerInfo;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects which server info links can be shown and opened, and the form they are shown in.</summary>
public class ServerInfoLinkRulesTests
{
    private readonly ServerInfoLinkRules rules = new();

    [Theory]
    [InlineData("https://discord.gg/example", "https://discord.gg/example")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("HTTPS://Example.COM/Path?Q=1#Top", "https://example.com/Path?Q=1#Top")]
    [InlineData("https://example.com:443/rules", "https://example.com/rules")]
    [InlineData("https://example.com:8443/rules", "https://example.com:8443/rules")]
    [InlineData("http://127.0.0.1:8080/status", "http://127.0.0.1:8080/status")]
    [InlineData("http://[::1]:8080/status", "http://[::1]:8080/status")]
    [InlineData("https://example.com/a\"b", "https://example.com/a%22b")]
    [InlineData("https://example.com/{x}|^`", "https://example.com/%7Bx%7D%7C%5E%60")]
    [InlineData("https://example.com/über?ä=ö", "https://example.com/%C3%BCber?%C3%A4=%C3%B6")]
    public void AbsoluteHttpAddresses_AreNormalized(string address, string expected)
    {
        Assert.True(rules.TryNormalize(address, out string normalized));
        Assert.Equal(expected, normalized);
    }

    // A look-alike host is shown in punycode, so it cannot pass for the real name.
    [Theory]
    [InlineData("https://bücher.example/pfad", "https://xn--bcher-kva.example/pfad")]
    [InlineData("https://d\u0456scord.gg/example", "https://xn--dscord-pvf.gg/example")]
    public void InternationalHosts_AreShownInPunycode(string address, string expected)
    {
        Assert.True(rules.TryNormalize(address, out string normalized));
        Assert.Equal(expected, normalized);
        Assert.All(normalized, character => Assert.InRange(character, '!', '~'));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:void(0)")]
    [InlineData("JavaScript://example.com/%0Avoid(0)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("file://server/share/file.txt")]
    [InlineData("ftp://example.com/file")]
    [InlineData("mailto:admin@example.com")]
    [InlineData("data:text/html,hello")]
    [InlineData("steam://connect/127.0.0.1")]
    [InlineData("/relative/path")]
    [InlineData("relative/path")]
    [InlineData("discord.gg/example")]
    [InlineData("//discord.gg/example")]
    [InlineData("http:example.com")]
    [InlineData("https:/example.com")]
    [InlineData("https:///example.com")]
    [InlineData("https://")]
    [InlineData("https://user@example.com/")]
    [InlineData("https://user:password@example.com/")]
    [InlineData("https://@example.com/")]
    [InlineData("https://discord.gg@example.com/")]
    [InlineData("https://example.com\\@discord.gg/")]
    [InlineData("https://exa mple.com/")]
    [InlineData("https://example.com/a b")]
    [InlineData(" https://example.com/")]
    [InlineData("https://example.com/ ")]
    public void EverythingElse_IsRejected(string address)
    {
        Assert.False(rules.TryNormalize(address, out string normalized));
        Assert.Equal(string.Empty, normalized);
    }

    // Built here rather than as theory data, which does not carry control characters and lone surrogates well.
    [Fact]
    public void InvisibleAndBrokenCharacters_AreRejected()
    {
        string[] inserted = { "\u00A0", "\t", "\n", "\r", "\u0007", "\u0000", "\u007F", "\u0085", "\u200B", "\u202E", "\uFEFF", "\ud83d", "\ude00", "\ude00\ud83d" };

        foreach (string character in inserted)
        {
            Assert.False(rules.TryNormalize("https://example.com/a" + character + "b", out _), ((int)character[0]).ToString("X4"));
            Assert.False(rules.TryNormalize("https://exa" + character + "mple.com/", out _), ((int)character[0]).ToString("X4"));
        }

        Assert.True(rules.TryNormalize("https://example.com/\uD83D\uDE00", out string pair));
        Assert.Equal("https://example.com/%F0%9F%98%80", pair);
    }

    [Fact]
    public void LengthCap_IsInclusive()
    {
        string prefix = "https://example.com/";
        string atCap = prefix + new string('a', ServerInfoLimits.MaxLinkUrlLength - prefix.Length);

        Assert.True(rules.TryNormalize(atCap, out string normalized));
        Assert.Equal(atCap, normalized);
        Assert.False(rules.TryNormalize(atCap + "a", out _));
    }

    // Escaping can grow an address past the cap, and the client would then drop what the server sent.
    [Fact]
    public void AddressThatGrowsPastTheCapWhenNormalized_IsRejected()
    {
        string prefix = "https://example.com/";
        string address = prefix + new string('"', 200);

        Assert.True(address.Length <= ServerInfoLimits.MaxLinkUrlLength);
        Assert.False(rules.TryNormalize(address, out _));
    }

    // The server sends the normalized address and the client checks it again, so both must agree.
    [Theory]
    [InlineData("https://bücher.example/pfad?q=ä#ü")]
    [InlineData("HTTPS://Example.COM:443/Path")]
    [InlineData("https://example.com/a\"b")]
    [InlineData("http://[::1]:8080/x")]
    public void NormalizedAddress_NormalizesToItself(string address)
    {
        Assert.True(rules.TryNormalize(address, out string once));
        Assert.True(rules.TryNormalize(once, out string twice));
        Assert.Equal(once, twice);
    }

    [Fact]
    public void NormalizedAddresses_NeverCarryShellQuotesOrSpaces()
    {
        string[] addresses =
        {
            "https://example.com/\"quoted\"", "https://example.com/?a=\"b\"&c=<d>", "https://example.com/#\"x\"",
            "https://example.com/%22already%22", "https://bücher.example/\"x\"",
        };

        foreach (string address in addresses)
        {
            Assert.True(rules.TryNormalize(address, out string normalized), address);
            Assert.DoesNotContain(normalized, character => character <= ' ' || character >= 0x7F || "\"<>\\^`{|}".Contains(character));
        }
    }
}
