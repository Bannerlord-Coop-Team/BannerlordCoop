namespace Common.Network.Session;

/// <summary>Display-safe metadata for one public standalone-server Steam lobby.</summary>
public class SteamLobbySummary
{
    public ulong LobbyId { get; set; }
    public int ProtocolVersion { get; set; }
    public string ModVersion { get; set; }
    public bool PasswordRequired { get; set; }

    /// <summary>
    /// Whether this client understands the lobby metadata and connection protocol. The existing
    /// module validation handshake separately rejects a different installed mod version.
    /// </summary>
    public bool IsCompatible => ProtocolVersion == SessionJoinInfo.CurrentVersion;
}
