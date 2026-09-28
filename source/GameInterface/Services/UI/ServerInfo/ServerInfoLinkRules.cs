using System;
using System.Globalization;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Decides which server info links may be shown and opened, on the server and again on each client.</summary>
public interface IServerInfoLinkRules
{
    /// <summary>
    /// Accepts an absolute http or https address whose host is a domain name, with no user info, no whitespace or
    /// control characters and no port 0, within <see cref="ServerInfoLimits.MaxLinkUrlLength"/>, and returns it in a
    /// plain ASCII form with the host in punycode. Anything else, IP addresses and names like localhost included, is
    /// rejected.
    /// </summary>
    bool TryNormalize(string address, out string normalized);
}

/// <inheritdoc cref="IServerInfoLinkRules"/>
public sealed class ServerInfoLinkRules : IServerInfoLinkRules
{
    private static readonly char[] AuthorityEnd = { '/', '?', '#' };

    public bool TryNormalize(string address, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrEmpty(address) || address.Length > ServerInfoLimits.MaxLinkUrlLength) return false;
        if (!HasOnlyAddressCharacters(address)) return false;
        if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;

        // Uri reports no user info for "https://@host" but keeps the "@", so the raw authority is checked too.
        int authorityStart = address.IndexOf("://", StringComparison.Ordinal) + 3;
        int authorityEnd = address.IndexOfAny(AuthorityEnd, authorityStart);
        string authority = authorityEnd < 0
            ? address.Substring(authorityStart)
            : address.Substring(authorityStart, authorityEnd - authorityStart);
        if (authority.Length == 0 || authority.IndexOf('@') >= 0) return false;

        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (uri.UserInfo.Length > 0) return false;

        string result;
        try
        {
            result = Normalize(uri);
        }
        catch (Exception exception) when (exception is UriFormatException || exception is ArgumentException)
        {
            return false;
        }

        if (result == null || result.Length > ServerInfoLimits.MaxLinkUrlLength || !IsPlainAscii(result)) return false;
        normalized = result;
        return true;
    }

    // Punycode shows a look-alike host for what it is, and the escaped path keeps the whole address ASCII.
    private static string Normalize(Uri uri)
    {
        // Community links use domain names, and an IP address can point the browser at the player's own machine or network.
        if (uri.HostNameType != UriHostNameType.Dns) return null;

        string host = uri.IdnHost;
        // No browser opens port 0, so it would only make a confusing address.
        if (!IsDomainName(host) || uri.Port == 0) return null;

        string port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        return uri.Scheme + "://" + host + port + uri.PathAndQuery + uri.Fragment;
    }

    // A name that ends in a number is an IP address to a browser, a single name like localhost or one under .localhost
    // reaches the player's own machine or network, and a trailing dot makes one site look like two.
    private static bool IsDomainName(string host)
    {
        if (string.IsNullOrEmpty(host) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)) return false;

        int lastDot = host.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == host.Length - 1) return false;

        char first = char.ToLowerInvariant(host[lastDot + 1]);
        return first >= 'a' && first <= 'z';
    }

    private static bool HasOnlyAddressCharacters(string address)
    {
        for (int i = 0; i < address.Length; i++)
        {
            char character = address[i];
            if (char.IsControl(character) || char.IsWhiteSpace(character) || character == '\\') return false;
            // Invisible marks like U+200B or U+202E can make one address look like another.
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format) return false;
            if (char.IsHighSurrogate(character))
            {
                if (i + 1 >= address.Length || !char.IsLowSurrogate(address[i + 1])) return false;
                i++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return false;
            }
        }

        return true;
    }

    // The address is handed to the shell, so quotes and other characters a URI must escape are refused.
    private static bool IsPlainAscii(string address)
    {
        foreach (char character in address)
        {
            if (character <= ' ' || character >= 0x7F) return false;
            if ("\"<>\\^`{|}".IndexOf(character) >= 0) return false;
        }

        return true;
    }
}
