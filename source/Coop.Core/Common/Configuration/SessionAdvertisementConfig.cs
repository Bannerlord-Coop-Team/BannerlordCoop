using Common.Network.Session;

namespace Coop.Core.Common.Configuration;

/// <summary>
/// The session advertisement choices carried into the client or server container.
/// </summary>
public class SessionAdvertisementConfig
{
    public bool EnableSteamInvites { get; set; }

    /// <summary>Who can discover a standalone server through Steam.</summary>
    public ServerVisibility Visibility { get; set; } = ServerVisibility.Public;
}
