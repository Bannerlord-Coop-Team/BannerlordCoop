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
                    Logger.Trace("Broadcast SyncCall: {}", Call);
                    m_EnvironmentServer.EventQueue.Add(serverRoom, this);
                }
                else if (room is RailClientRoom clientRoom)
                {
                    Logger.Trace("SyncCall: {}", Call);
                    method.CallOriginal(
                        ArgumentFactory.Resolve(m_EnvironmentClient.Store, Call.Instance),
                        ArgumentFactory.Resolve(m_EnvironmentClient.Store, Call.Arguments));
                    Free();
                }
            }
            else
            {
                Logger.Warn("Unknown SyncCall: ", Call);
            }
        }
    }
}
