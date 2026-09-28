using System;
using System.Globalization;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Decides which server info links may be shown and opened, on the server and again on each client.</summary>
public interface IServerInfoLinkRules
{
    /// <summary>
    /// Accepts an absolute http or https address with a host, no user info, no whitespace or control
    /// characters, within <see cref="ServerInfoLimits.MaxLinkUrlLength"/>, and returns it in a plain
    /// ASCII form with the host in punycode. Anything else is rejected.
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
        string host;
        switch (uri.HostNameType)
        {
            case UriHostNameType.Dns:
                host = uri.IdnHost;
                break;
            case UriHostNameType.IPv4:
            case UriHostNameType.IPv6:
                host = uri.Host;
                break;
            default:
                return null;
        }

        if (string.IsNullOrEmpty(host)) return null;
        string port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        return uri.Scheme + "://" + host + port + uri.PathAndQuery + uri.Fragment;
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
