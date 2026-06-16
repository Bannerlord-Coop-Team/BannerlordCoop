using System.Collections.Generic;
using System.Net;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// Server-side record of a single P2P mission instance: the group of co-located players sharing one
/// settlement interior. The id is derived client-side from settlement + location; the server holds the
/// P2P socket endpoints each peer presents for NAT introduction.
/// </summary>
internal class MissionInstance
{
    public string Id { get; }

    /// <summary>
    /// P2P socket endpoints presented via NAT-introduction requests.
    /// </summary>
    public List<Endpoints> PunchEndpoints { get; } = new List<Endpoints>();

    public MissionInstance(string id)
    {
        Id = id;
    }

    /// <summary>The internal (LAN) and external (WAN) endpoints a peer presents for NAT introduction.</summary>
    public readonly struct Endpoints
    {
        public readonly IPEndPoint Internal;
        public readonly IPEndPoint External;

        public Endpoints(IPEndPoint @internal, IPEndPoint external)
        {
            Internal = @internal;
            External = external;
        }
    }
}
