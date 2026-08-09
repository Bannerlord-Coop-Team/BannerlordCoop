using System.Threading.Tasks;

namespace Common.Network.Session;

/// <summary>
/// Turns advertised join info into the endpoint the transport actually dials.
/// The direct implementation passes the info through unchanged; a tunneling
/// implementation can stand up its link first and return a local endpoint.
/// </summary>
public interface IJoinEndpointPreparer
{
    Task<SessionJoinInfo> PrepareAsync(SessionJoinInfo info);
}

/// <summary>Provider tunnel preparer with an explicit process-lifetime teardown hook.</summary>
public interface ITunnelJoinEndpointPreparer : IJoinEndpointPreparer
{
    string Provider { get; }
    void TearDown();
}
