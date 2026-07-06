namespace Coop.Core.Common.Configuration;

/// <summary>
/// The hosting player's choices for advertising the session, carried from the connect
/// screen into the client container.
/// </summary>
public class SessionAdvertisementConfig
{
    public bool EnableSteamInvites { get; set; }

    /// <summary>
    /// The externally reachable address friends should dial (the host's public IP or
    /// domain). Empty when the host has not provided one.
    /// </summary>
    public string PublicAddress { get; set; } = string.Empty;
}
