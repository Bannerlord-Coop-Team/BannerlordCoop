using System;
using System.Collections.Generic;
using System.Globalization;

namespace Common.Network.Session;

/// <summary>Encodes provider-neutral session metadata into lobby key/value data.</summary>
public static class SessionListingDataCodec
{
    public const string VersionKey = "coop_version";
    public const string AddressKey = "coop_address";
    public const string PortKey = "coop_port";
    public const string TunnelProviderKey = "coop_tunnel_provider";
    public const string TunnelPeerIdKey = "coop_tunnel_peer_id";
    public const string OwnerNameKey = "coop_owner_name";
    public const string ModVersionKey = "coop_mod_version";
    public const string PasswordRequiredKey = "coop_password_required";
    public const string ConnectedPlayersKey = "coop_connected_players";
    public const string ListingTypeKey = "coop_lobby_type";
    public const string VisibilityKey = "coop_visibility";
    public const string AdvertisementExpiresAtKey = "coop_advertisement_expires_at";
    public const string DedicatedListingType = "standalone";
    public const string HiddenDedicatedListingType = "standalone_hidden";
    public const string PlayerListingType = "player";

    public static string EncodeVisibility(ServerVisibility visibility) => visibility switch
    {
        ServerVisibility.Public => "public",
        ServerVisibility.FriendsOnly => "friends_only",
        ServerVisibility.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(visibility)),
    };

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

    public static string EncodeAdvertisementExpiry(uint expiresAt) =>
        expiresAt.ToString(CultureInfo.InvariantCulture);

    public static bool TryDecodeAdvertisementExpiry(string value, out uint expiresAt) =>
        uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out expiresAt);

    public static IReadOnlyDictionary<string, string> Encode(SessionJoinInfo info)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));

        return new Dictionary<string, string>
        {
            [VersionKey] = info.Version.ToString(CultureInfo.InvariantCulture),
            [AddressKey] = info.Address ?? string.Empty,
            [PortKey] = info.Port.ToString(CultureInfo.InvariantCulture),
            [TunnelProviderKey] = info.TunnelTarget.Provider ?? string.Empty,
            [TunnelPeerIdKey] = info.TunnelTarget.UserId ?? string.Empty,
            [ModVersionKey] = info.ModVersion ?? string.Empty,
            [PasswordRequiredKey] = info.PasswordRequired ? "1" : "0",
            [ConnectedPlayersKey] = Math.Max(0, info.ConnectedPlayers).ToString(CultureInfo.InvariantCulture),
            [ListingTypeKey] = info.DedicatedServer
                ? (info.Discoverable ? DedicatedListingType : HiddenDedicatedListingType)
                : PlayerListingType,
        };
    }

    public static bool TryDecode(
        Func<string, string> readValue,
        out SessionJoinInfo info,
        out string error)
    {
        if (readValue == null) throw new ArgumentNullException(nameof(readValue));

        info = null;
        error = null;

        string versionText = readValue(VersionKey);
        if (string.IsNullOrEmpty(versionText) ||
            !int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
        {
            error = "This listing is not a co-op session";
            return false;
        }

        if (version != SessionJoinInfo.CurrentVersion)
        {
            error = version > SessionJoinInfo.CurrentVersion
                ? "The host is running a newer co-op version; update your mod to join"
                : "The host is running an older co-op version; ask them to update the mod";
            return false;
        }

        string modVersion = readValue(ModVersionKey);
        if (!ModInformation.MatchesBuildVersion(modVersion))
        {
            error = string.IsNullOrEmpty(modVersion)
                ? "The host did not advertise a co-op mod version"
                : $"The host is running co-op mod {modVersion}; " +
                  $"this client is running {ModInformation.BuildVersion}";
            return false;
        }

        if (!int.TryParse(
                readValue(PortKey),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int port) ||
            port < 1 || port > 65535)
        {
            error = "The co-op listing has no valid port";
            return false;
        }

        var tunnelTarget = new PlatformIdentity(
            readValue(TunnelProviderKey),
            readValue(TunnelPeerIdKey));

        int.TryParse(
            readValue(ConnectedPlayersKey),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int connectedPlayers);
        connectedPlayers = Math.Max(0, connectedPlayers);

        string listingType = readValue(ListingTypeKey);
        bool dedicatedServer =
            string.Equals(listingType, DedicatedListingType, StringComparison.Ordinal) ||
            string.Equals(listingType, HiddenDedicatedListingType, StringComparison.Ordinal);

        info = new SessionJoinInfo
        {
            Version = version,
            Address = readValue(AddressKey),
            Port = port,
            TunnelTarget = tunnelTarget,
            DedicatedServer = dedicatedServer,
            ModVersion = modVersion,
            PasswordRequired = readValue(PasswordRequiredKey) == "1",
            ConnectedPlayers = connectedPlayers,
            Discoverable = !string.Equals(
                listingType,
                HiddenDedicatedListingType,
                StringComparison.Ordinal),
        };
        return true;
    }
}
