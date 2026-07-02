using Common.Messaging;

namespace Common.Network.Session.Messages;

/// <summary>
/// A discovery mechanism resolved everything needed to join a session.
/// </summary>
public record SessionJoinInfoResolved : ICommand
{
    public SessionJoinInfo JoinInfo { get; }

    public SessionJoinInfoResolved(SessionJoinInfo joinInfo)
    {
        JoinInfo = joinInfo;
    }
}
