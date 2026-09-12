using Common.Network.Session;

namespace Coop.Core.Common.Session;

/// <summary>
/// Advertiser for plain direct-IP hosting: nothing is published anywhere.
/// </summary>
public class NoopSessionAdvertiser : ISessionAdvertiser
{
    public bool IsAdvertising => false;
    public bool CanInviteFriends => false;

    public void Advertise(SessionJoinInfo info)
    {
    }

    public void StopAdvertising()
    {
    }

    public bool InviteFriends() => false;

    public void Dispose()
    {
    }
}
