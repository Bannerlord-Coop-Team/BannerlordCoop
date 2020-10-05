using JetBrains.Annotations;
using NLog;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using RailgunNet.Util;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     RailEvent used to initiate remote procedure calls. The way this event is processed
    ///     differs between client and server:
    ///     - The server acts as a broadcasting relay station. Effectively receiving the event
    ///     and forwarding it to all clients (INCLUDING the client that sent the event!).
    ///     Note that the server will delay event execution until all arguments have been
    ///     transferred to all clients.
    ///     - Clients will resolve the call and arguments locally and execute it.
    /// </summary>
    public class EventMethodCall : RailEvent
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [OnlyIn(Component.Client)]
        [CanBeNull]
        private readonly IEnvironmentClient m_EnvironmentClient;

        [OnlyIn(Component.Server)]
        [CanBeNull]
        private readonly IEnvironmentServer m_EnvironmentServer;

        [OnlyIn(Component.Client)]
        public EventMethodCall([NotNull] IEnvironmentClient environment)
        {
            m_EnvironmentClient = environment;
        }

        [OnlyIn(Component.Server)]
        public EventMethodCall([NotNull] IEnvironmentServer environment)
        {
            m_EnvironmentServer = environment;
        }

        [EventData] public MethodCall Call { get; set; }

        protected override void Execute(RailRoom room, RailController sender)
        {
            if (MethodRegistry.IdToMethod.TryGetValue(Call.Id, out MethodAccess method))
            {
                if (room is RailServerRoom serverRoom)
                {
                    Logger.Trace("[{eventId}] Broadcast SyncCall: {call}", EventId, Call);
                    m_EnvironmentServer.EventQueue.Add(serverRoom, this);
                }
                else if (room is RailClientRoom)
                {
                    Logger.Trace("[{eventId}] SyncCall: {call}", EventId, Call);
                    // TODO: The call is not synchronized to a campaign time at this point. We probably want an execution queue of some sorts that executes the call at the right point in time.
                    method.CallOriginal(
                        ArgumentFactory.Resolve(m_EnvironmentClient.Store, Call.Instance),
                        ArgumentFactory.Resolve(m_EnvironmentClient.Store, Call.Arguments));
                    PendingRequests.Instance.Remove(Call);
                }
            }
            else
            {
                Logger.Warn("[{eventId}] Unknown SyncCall: {call}", EventId, Call);
            }
        }
    }
}
