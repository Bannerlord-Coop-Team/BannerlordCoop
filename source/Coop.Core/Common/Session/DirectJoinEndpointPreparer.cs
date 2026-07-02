using Common.Network.Session;
using System.Threading.Tasks;

namespace Coop.Core.Common.Session;

/// <summary>
/// Direct transport: the advertised address is dialed as-is.
/// </summary>
public class DirectJoinEndpointPreparer : IJoinEndpointPreparer
{
    public Task<SessionJoinInfo> PrepareAsync(SessionJoinInfo info) => Task.FromResult(info);
}
