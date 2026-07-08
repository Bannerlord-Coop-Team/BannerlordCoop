using Common.Network.Session;
using System;
using System.Collections.Generic;

namespace Coop.Steam;

/// <summary>
/// Encodes <see cref="SessionJoinInfo"/> as Steam lobby key/value data and back.
/// </summary>
public static class LobbyDataCodec
{
    public const string VersionKey = "coop_version";
    public const string AddressKey = "coop_address";
    public const string PortKey = "coop_port";

    public static IReadOnlyDictionary<string, string> Encode(SessionJoinInfo info)
    {
        return new Dictionary<string, string>
        {
            [VersionKey] = info.Version.ToString(),
            [AddressKey] = info.Address ?? string.Empty,
            [PortKey] = info.Port.ToString(),
        };
    }

    public static bool TryDecode(Func<string, string> readValue, out SessionJoinInfo info, out string error)
    {
        info = null;
        error = null;

        var versionText = readValue(VersionKey);
        if (string.IsNullOrEmpty(versionText) || !int.TryParse(versionText, out var version))
        {
            error = "This Steam lobby is not a co-op session";
            return false;
        }

        if (version > SessionJoinInfo.CurrentVersion)
        {
            error = "The host is running a newer co-op version; update your mod to join";
            return false;
        }

        if (!int.TryParse(readValue(PortKey), out var port) || port < 1 || port > 65535)
        {
            error = "The co-op lobby has no valid port";
            return false;
        }

        info = new SessionJoinInfo
        {
            Version = version,
            Address = readValue(AddressKey),
            Port = port,
        };
        return true;
    }
}
