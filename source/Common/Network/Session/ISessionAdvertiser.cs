using System;

namespace Common.Network.Session;

/// <summary>
/// Player-facing hints shared by every surface that reports invite availability.
/// </summary>
public static class SessionInviteText
{
    public const string OverlayUnavailableHint =
        "Steam overlay unavailable; friends can right-click you in the Steam friends list and choose Join Game";
}

/// <summary>
/// Publishes a joinable session to a discovery mechanism (a Steam lobby;
/// no-op for plain direct-IP hosting) so friends can join without typing an address.
/// </summary>
public interface ISessionAdvertiser : IDisposable
{
    bool IsAdvertising { get; }

    /// <summary>Whether this client can invite friends to its current Steam lobby.</summary>
    bool CanInviteFriends { get; }

    /// <summary>
    /// Starts or refreshes the advertisement. Safe to call again with updated info.
    /// </summary>
    void Advertise(SessionJoinInfo info);

    void StopAdvertising();

    /// <summary>
    /// Opens the platform's invite UI. Returns false when no invite UI could be shown
    /// (no active lobby or no overlay), so the caller can surface an alternative.
    /// </summary>
    bool InviteFriends();
}
