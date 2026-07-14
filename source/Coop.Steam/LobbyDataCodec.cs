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
    public const string ServerSteamIdKey = "coop_server_steamid";
    public const string OwnerNameKey = "coop_owner_name";
    public const string ModVersionKey = "coop_mod_version";
    public const string PasswordRequiredKey = "coop_password_required";
    public const string ConnectedPlayersKey = "coop_connected_players";
    public const string LobbyTypeKey = "coop_lobby_type";
    public const string VisibilityKey = "coop_visibility";
    public const string StandaloneLobbyType = "standalone";
    public const string HiddenStandaloneLobbyType = "standalone_hidden";
    public const string PlayerLobbyType = "player";

    public static string EncodeVisibility(ServerVisibility visibility) => visibility switch
    {
        ServerVisibility.Public => "public",
        ServerVisibility.FriendsOnly => "friends_only",
        ServerVisibility.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(visibility)),
    };

    /// <summary>
    /// Reads standalone-server discovery visibility. Missing metadata remains Public so clients
    /// can still discover lobbies advertised by builds from before this field existed.
    /// </summary>
    public static bool TryDecodeVisibility(string value, out ServerVisibility visibility)
    {
        if (string.IsNullOrEmpty(value) || string.Equals(value, "public", StringComparison.Ordinal))
        {
            visibility = ServerVisibility.Public;
            return true;
        }

        if (string.Equals(value, "friends_only", StringComparison.Ordinal))
        {
            visibility = ServerVisibility.FriendsOnly;
            return true;
        }

        if (string.Equals(value, "none", StringComparison.Ordinal))
        {
            visibility = ServerVisibility.None;
            return true;
        }

        visibility = ServerVisibility.None;
        return false;
    }

    public static IReadOnlyDictionary<string, string> Encode(SessionJoinInfo info)
    {
        return new Dictionary<string, string>
        {
            [VersionKey] = info.Version.ToString(),
            [AddressKey] = info.Address ?? string.Empty,
            [PortKey] = info.Port.ToString(),
            [ServerSteamIdKey] = info.ServerSteamId.ToString(),
            [ModVersionKey] = info.ModVersion ?? string.Empty,
            [PasswordRequiredKey] = info.PasswordRequired ? "1" : "0",
            [ConnectedPlayersKey] = Math.Max(0, info.ConnectedPlayers).ToString(),
            [LobbyTypeKey] = info.HasServerSteamId
                ? (info.Discoverable ? StandaloneLobbyType : HiddenStandaloneLobbyType)
                : PlayerLobbyType,
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

        var modVersion = readValue(ModVersionKey);
        if (!Common.ModInformation.MatchesBuildVersion(modVersion))
        {
            error = string.IsNullOrEmpty(modVersion)
                ? "The host did not advertise a co-op mod version"
                : $"The host is running co-op mod {modVersion}; " +
                  $"this client is running {Common.ModInformation.BuildVersion}";
            return false;
        }

        if (!int.TryParse(readValue(PortKey), out var port) || port < 1 || port > 65535)
        {
            error = "The co-op lobby has no valid port";
            return false;
        }

        // Absent (older lobby) or unparsable server id decodes to 0, which HasServerSteamId
        // reads as "player-hosted", so the joiner falls back to the lobby owner.
        ulong.TryParse(readValue(ServerSteamIdKey), out var serverSteamId);

        int.TryParse(readValue(ConnectedPlayersKey), out var connectedPlayers);
        connectedPlayers = Math.Max(0, connectedPlayers);

        info = new SessionJoinInfo
        {
            Version = version,
            Address = readValue(AddressKey),
            Port = port,
            ServerSteamId = serverSteamId,
            ModVersion = modVersion,
            PasswordRequired = readValue(PasswordRequiredKey) == "1",
            ConnectedPlayers = connectedPlayers,
            Discoverable = !string.Equals(
                readValue(LobbyTypeKey), HiddenStandaloneLobbyType, StringComparison.Ordinal),
        };
        return true;
    }
}
