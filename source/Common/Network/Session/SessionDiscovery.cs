using System;
using System.Collections.Generic;

namespace Common.Network.Session;

/// <summary>Process-wide provider capabilities selected once at mod load.</summary>
public static class SessionDiscovery
{
    public static ISessionProvider ClientProvider { get; set; }
    public static ISessionProvider ServerProvider { get; set; }

    public static bool ProviderAvailable => ClientProvider != null;

    public static ISessionBrowser Browser => ClientProvider?.Browser ?? UnavailableSessionBrowser.Instance;

    private sealed class UnavailableSessionBrowser : ISessionBrowser
    {
        public static readonly UnavailableSessionBrowser Instance = new UnavailableSessionBrowser();

        public string Provider => string.Empty;
        public string DisplayName => string.Empty;
        public bool IsAvailable => false;

        public void RequestSessions(Action<IReadOnlyList<SessionListing>, string> onCompleted)
        {
            onCompleted(Array.Empty<SessionListing>(), "Platform session discovery is unavailable");
        }
    }
}
