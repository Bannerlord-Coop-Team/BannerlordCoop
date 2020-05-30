using System;
using System.Diagnostics;
using System.Linq;
using Common;
using JetBrains.Annotations;
using RailgunNet.Connection.Client;
using RailgunNet.System.Types;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    public class MethodCallSyncHandler
    {
        private bool m_bIsRegistered;

        public MethodCallSyncHandler([NotNull] SyncMethod method)
        {
            Method = method;
            Register();
        }

        public Statistics Stats { get; } = new Statistics();
        public SyncMethod Method { get; }

        [Conditional("DEBUG")]
        private void Trace(MethodCall call, RailClientRoom room)
        {
            Stats.History.Push(
                new Statistics.Trace
                {
                    Call = call,
                    Tick = room.Tick
                });
        }

        private void Register()
        {
            if (m_bIsRegistered)
            {
                return;
            }

            Method.SetGlobalHandler(
                (instance, args) =>
                {
                    if (args is object[] objects)
                    {
                        CoopClient.Instance.Persistence?.Room.RaiseEvent<EventMethodCall>(
                            evt =>
                            {
                                evt.Call = new MethodCall
                                {
                                    Id = Method.Id,
                                    Instance = ArgumentFactory.Create(instance),
                                    Arguments =
                                        objects.Select(o => ArgumentFactory.Create(o))
                                               .ToList()
                                };
                                Trace(evt.Call, CoopClient.Instance.Persistence.Room);
                            });
                    }
                    else
                    {
                        throw new ArgumentNullException(nameof(args), "Unexpected argument type.");
                    }
                });
            m_bIsRegistered = true;
        }

        private void Unregister()
        {
            if (!m_bIsRegistered)
            {
                return;
            }

            Method.RemoveGlobalHandler();
        }

        ~MethodCallSyncHandler()
        {
            Unregister();
        }

        public class Statistics
        {
            public DropoutStack<Trace> History = new DropoutStack<Trace>(10);

            public struct Trace
            {
                public MethodCall Call { get; set; }
                public Tick Tick { get; set; }
            }
        }
    }
}
