using System;

namespace Common.Network.Session;

/// <summary>Opaque provider-scoped identifier for an advertised session.</summary>
public readonly struct SessionListingId : IEquatable<SessionListingId>
{
    public string Provider { get; }
    public string Value { get; }

    public bool IsValid => !string.IsNullOrEmpty(Provider) && !string.IsNullOrEmpty(Value);

    public SessionListingId(string provider, string value)
    {
        Provider = provider?.Trim().ToLowerInvariant() ?? string.Empty;
        Value = value?.Trim() ?? string.Empty;
    }

    public bool Equals(SessionListingId other) =>
        string.Equals(Provider, other.Provider, StringComparison.Ordinal) &&
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is SessionListingId other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Provider?.GetHashCode() ?? 0) * 397) ^ (Value?.GetHashCode() ?? 0);
        }
    }

    public override string ToString() => IsValid ? Provider + ":" + Value : string.Empty;

    public static bool operator ==(SessionListingId left, SessionListingId right) => left.Equals(right);
    public static bool operator !=(SessionListingId left, SessionListingId right) => !left.Equals(right);
}

/// <summary>Display-safe metadata for one provider-hosted co-op session.</summary>
public class SessionListing
{
    public SessionListingId Id { get; set; }
    public string OwnerName { get; set; }
    public int ProtocolVersion { get; set; }
    public string ModVersion { get; set; }
    public bool PasswordRequired { get; set; }
    public int ConnectedPlayers { get; set; }

    public bool IsCompatible => ProtocolVersion == SessionJoinInfo.CurrentVersion &&
        ModInformation.MatchesBuildVersion(ModVersion);
}
