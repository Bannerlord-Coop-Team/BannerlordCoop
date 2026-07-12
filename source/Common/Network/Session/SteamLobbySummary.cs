namespace Common.Network.Session;

/// <summary>Display-safe metadata for one public standalone-server Steam lobby.</summary>
public class SteamLobbySummary
{
    public ulong LobbyId { get; set; }
    public int ProtocolVersion { get; set; }
    public string ModVersion { get; set; }
    public bool PasswordRequired { get; set; }

    /// <summary>
    /// Whether this client uses the same lobby protocol and exact mod build as the host.
    /// </summary>
    public bool IsCompatible => ProtocolVersion == SessionJoinInfo.CurrentVersion &&
        ModInformation.MatchesBuildVersion(ModVersion);
}
