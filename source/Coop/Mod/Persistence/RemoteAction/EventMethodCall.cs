using System.Collections.Generic;
using JetBrains.Annotations;
using NLog;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using RailgunNet.Util;
using RemoteAction;
using Sync;
using Sync.Behaviour;
using Sync.Call;

namespace Coop.Mod.Persistence.RemoteAction
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
    public class EventMethodCall : EventActionBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [OnlyIn(Component.Client)] [CanBeNull] private readonly IEnvironmentClient m_EnvironmentClient;

        [OnlyIn(Component.Server)] [CanBeNull] private readonly IEnvironmentServer m_EnvironmentServer;

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
        public override IEnumerable<Argument> Arguments => Call.Arguments;

        public override bool IsValid()
        {
            return Call.IsValid();
        }

        protected override void Execute(RailRoom room, RailController sender)
        {
            if (Sync.Registry.IdToInvokable.TryGetValue(Call.Id, out Invokable method))
            {
                if (room is RailServerRoom serverRoom)
                {
                    if (!IsValid())
                    {
                        ActionValidatorRegistry.TryGet(Call.Id, out IActionValidator validator);
                        Logger.Info("[{EventId}] Broadcast SyncCall '{Call}' rejected by {Validator}: {Reason}", EventId, Call, validator.GetType().Name, validator.GetReasonForRejection());
                        return;
                    }

                    Logger.Trace("[{EventId}] Broadcast SyncCall: {Call}", EventId, Call);
                    m_EnvironmentServer.EventQueue.Add(serverRoom, this);
                }
                else if (room is RailClientRoom)
                {
                    Logger.Trace("[{EventId}] SyncCall: {Call}", EventId, Call);
                    // TODO: The call is not synchronized to a campaign time at this point. We probably want an execution queue of some sorts that executes the call at the right point in time.
                    method.Invoke(
                        EOriginator.RemoteAuthority,
                        ArgumentFactory.Resolve(m_EnvironmentClient.Store, Call.Instance),
                        ArgumentFactory.Resolve(m_EnvironmentClient.Store, Call.Arguments));
                }
            }
            else
            {
                Logger.Warn("[{eventId}] Unknown SyncCall: {call}", EventId, Call);
            }
        }
    }
}