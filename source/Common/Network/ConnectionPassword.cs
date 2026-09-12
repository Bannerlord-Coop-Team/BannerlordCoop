using System;

namespace Common.Network;

/// <summary>Machine-readable reasons a server can attach to a rejected connection.</summary>
public enum ConnectionRejectCode : byte
{
    None = 0,
    IncorrectPassword = 1,
}

/// <summary>
/// Shared rules for the optional password carried in LiteNetLib's connection request data.
/// </summary>
public static class ConnectionPassword
{
    public const int MaxLength = 128;

    public static bool IsValid(string password) => password == null || password.Length <= MaxLength;

    public static bool IsAccepted(string configuredPassword, string suppliedPassword)
    {
        if (string.IsNullOrEmpty(configuredPassword)) return true;
        return string.Equals(configuredPassword, suppliedPassword, StringComparison.Ordinal);
    }
}
