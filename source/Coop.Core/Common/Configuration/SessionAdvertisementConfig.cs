using Common.Network.Session;

namespace Coop.Core.Common.Configuration;

/// <summary>
/// The session advertisement choices carried into the client or server container.
/// </summary>
public class SessionAdvertisementConfig
{
    public bool EnablePlatformInvites { get; set; }

    /// <summary>Who can discover a standalone server through its active storefront.</summary>
    public ServerVisibility Visibility { get; set; } = ServerVisibility.Public;
}
