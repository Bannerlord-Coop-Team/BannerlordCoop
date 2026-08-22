using System;
using System.Collections.Generic;

namespace Common.Network.Session;

/// <summary>Lists sessions from one storefront without exposing its SDK types.</summary>
public interface ISessionBrowser
{
    string Provider { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }

    void RequestSessions(Action<IReadOnlyList<SessionListing>, string> onCompleted);
}
