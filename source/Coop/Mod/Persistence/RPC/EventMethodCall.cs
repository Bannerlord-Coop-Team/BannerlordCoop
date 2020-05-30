using NLog;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    public class EventMethodCall : RailEvent
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [EventData] public MethodCall Call { get; set; }

        protected override void Execute(RailRoom room, RailController sender)
        {
            if (MethodRegistry.IdToMethod.TryGetValue(Call.Id, out SyncMethod method))
            {
                if (room is RailServerRoom serverRoom)
                {
                    Logger.Trace("Broadcast SyncCall: ", Call);
                    serverRoom.BroadcastEvent(this);
                }
                else if (room is RailClientRoom clientRoom)
                {
                    Logger.Trace("SyncCall: ", Call);
                    method.CallOriginal(
                        clientRoom.Resolve(Call.Instance),
                        clientRoom.Resolve(Call.Arguments));
                }
            }
            else
            {
                Logger.Warn("Unknown SyncCall: ", Call);
            }
        }
    }
}
