using System;
using System.Net;

namespace Common.Network.Session;

/// <summary>Resolves an authenticated peer from the primary source before consulting a fallback.</summary>
public sealed class FallbackAuthenticatedPeerIdentityResolver : IAuthenticatedPeerIdentityResolver
{
    private readonly IAuthenticatedPeerIdentityResolver primary;
    private readonly IAuthenticatedPeerIdentityResolver fallback;

    public FallbackAuthenticatedPeerIdentityResolver(
        IAuthenticatedPeerIdentityResolver primary,
        IAuthenticatedPeerIdentityResolver fallback)
    {
        if (primary == null) throw new ArgumentNullException(nameof(primary));
        if (fallback == null) throw new ArgumentNullException(nameof(fallback));

        this.primary = primary;
        this.fallback = fallback;
    }

    public bool TryGetIdentity(IPEndPoint serverPeerEndpoint, out PlatformIdentity identity)
    {
        return primary.TryGetIdentity(serverPeerEndpoint, out identity) ||
            fallback.TryGetIdentity(serverPeerEndpoint, out identity);
    }
}
