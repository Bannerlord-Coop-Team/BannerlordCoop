using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Common;
using JetBrains.Annotations;
using RailgunNet.Connection.Client;
using RailgunNet.System.Types;
using Sync;
using Sync.Store;

namespace Coop.Mod.Persistence.RPC
{
    public class MethodCallSyncHandler
    {
        private bool m_IsRegistered;

        public MethodCallSyncHandler([NotNull] MethodAccess methodAccess)
        {
            MethodAccess = methodAccess;
            Register();
        }

        public Statistics Stats { get; } = new Statistics();
        public MethodAccess MethodAccess { get; }

        private static RemoteStore Store => CoopClient.Instance.SyncedObjectStore;

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
            if (m_IsRegistered)
            {
                return;
            }

            MethodAccess.SetGlobalHandler(
                (instance, args) =>
                {
                    if (args is object[] objects)
                    {
                        CoopClient.Instance.Persistence?.Room.RaiseEvent<EventMethodCall>(
                            evt =>
                            {
                                evt.Call = new MethodCall
                                {
                                    Id = MethodAccess.Id,
                                    Instance = ArgumentFactory.Create(
                                        Store,
                                        instance,
                                        false),
                                    Arguments = ProduceArguments(objects)
                                };
                                Trace(evt.Call, CoopClient.Instance.Persistence.Room);
                            });
                    }
                    else
                    {
                        throw new ArgumentNullException(nameof(args), "Unexpected argument type.");
                    }
                });
            m_IsRegistered = true;
        }

        private List<Argument> ProduceArguments(IEnumerable<object> objects)
        {
            bool bTransferByValue =
                MethodAccess.Flags.HasFlag(EMethodPatchFlag.TransferArgumentsByValue);
            return objects.Select(obj => ArgumentFactory.Create(Store, obj, bTransferByValue))
                          .ToList();
        }

        private void Unregister()
        {
            if (!m_IsRegistered)
            {
                return;
            }

            MethodAccess.RemoveGlobalHandler();
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
