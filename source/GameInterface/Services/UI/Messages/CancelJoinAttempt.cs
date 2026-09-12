using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

/// <summary>
/// Player request to abandon a join attempt that has not connected yet, ending the connect
/// retry loop that would otherwise redial forever.
/// </summary>
public record CancelJoinAttempt : ICommand
{
}
