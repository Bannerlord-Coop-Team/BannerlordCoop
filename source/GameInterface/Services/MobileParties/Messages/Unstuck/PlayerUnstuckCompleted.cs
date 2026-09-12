using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Unstuck;

/// <summary>
/// Published on the requesting client after the server's unstuck result was applied locally.
/// Carries the combined server + local action report.
/// </summary>
internal readonly struct PlayerUnstuckCompleted : IEvent
{
    public string PartyId { get; }
    public string[] Actions { get; }

    public PlayerUnstuckCompleted(string partyId, string[] actions)
    {
        PartyId = partyId;
        Actions = actions;
    }
}
