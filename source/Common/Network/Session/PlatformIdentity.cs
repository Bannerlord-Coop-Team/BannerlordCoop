using System;
using System.Globalization;

namespace Common.Network.Session;

/// <summary>Provider-scoped identity supplied by a platform networking adapter.</summary>
public readonly struct PlatformIdentity : IEquatable<PlatformIdentity>
{
    public string Provider { get; }
    public string UserId { get; }

    public bool IsValid => !string.IsNullOrEmpty(Provider) && !string.IsNullOrEmpty(UserId);

    public bool IsStorefrontIdentity =>
        string.Equals(Provider, "steam", StringComparison.Ordinal) ||
        string.Equals(Provider, "gog", StringComparison.Ordinal);

    public string ControllerId => IsValid ? Provider + ":" + UserId : string.Empty;

    public PlatformIdentity(string provider, string userId)
    {
        Provider = NormalizeProvider(provider);
        UserId = userId?.Trim() ?? string.Empty;
    }

    public bool Equals(PlatformIdentity other) =>
        string.Equals(Provider, other.Provider, StringComparison.Ordinal) &&
        string.Equals(UserId, other.UserId, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is PlatformIdentity other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Provider?.GetHashCode() ?? 0) * 397) ^ (UserId?.GetHashCode() ?? 0);
        }
    }

    public override string ToString() => ControllerId;

    public static bool operator ==(PlatformIdentity left, PlatformIdentity right) => left.Equals(right);
    public static bool operator !=(PlatformIdentity left, PlatformIdentity right) => !left.Equals(right);

    public static bool TryParseControllerId(string controllerId, out PlatformIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(controllerId)) return false;

        int separator = controllerId.IndexOf(':');
        if (separator <= 0 || separator == controllerId.Length - 1) return false;

        identity = new PlatformIdentity(
            controllerId.Substring(0, separator),
            controllerId.Substring(separator + 1));
        return identity.IsValid;
    }

    public static bool TryMigrateLegacySteamControllerId(
        string controllerId,
        out string migratedControllerId)
    {
        migratedControllerId = controllerId;
        if (TryParseControllerId(controllerId, out _) ||
            !ulong.TryParse(controllerId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        migratedControllerId = new PlatformIdentity("steam", controllerId).ControllerId;
        return true;
    }

    private static string NormalizeProvider(string provider) =>
        string.IsNullOrWhiteSpace(provider)
            ? string.Empty
            : provider.Trim().ToLowerInvariant();
}
