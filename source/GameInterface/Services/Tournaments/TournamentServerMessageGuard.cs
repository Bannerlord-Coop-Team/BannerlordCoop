using Common.Network;
using LiteNetLib;
using System.Collections.Generic;

namespace GameInterface.Services.Tournaments;

internal static class TournamentServerMessageGuard
{
    public static bool IsTrusted(object source) => source is NetPeer;

    public static bool IsTrusted(object source, IEnumerable<IRelayNetwork> _) =>
        IsTrusted(source);
}
