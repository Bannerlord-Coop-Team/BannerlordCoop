using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Common;
using JetBrains.Annotations;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.System.Types;
using RailgunNet.Util;
using Sync;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Registers a global call handler for a <see cref="MethodAccess" /> that sends an
    ///     <see cref="EventMethodCall" /> to the server.
    /// </summary>
    [OnlyIn(Component.Client)]
    public class MethodCallSyncHandler
    {
        [NotNull] private readonly IClientAccess m_ClientAccess;
        private bool m_IsRegistered;

        public MethodCallSyncHandler(
            [NotNull] MethodAccess methodAccess,
            [NotNull] IClientAccess access)
        {
            MethodAccess = methodAccess;
            m_ClientAccess = access;
            Register();
        }

        public Statistics Stats { get; } = new Statistics();
        public MethodAccess MethodAccess { get; }

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
                        m_ClientAccess.GetRoom()
                                      ?.RaiseEvent<EventMethodCall>(
                                          evt =>
                                          {
                                              evt.Call = new MethodCall
                                              {
                                                  Id = MethodAccess.Id,
                                                  Instance = ArgumentFactory.Create(
                                                      m_ClientAccess.GetStore(),
                                                      instance,
                                                      false),
                                                  Arguments = ProduceArguments(objects)
                                              };
                                              Trace(evt.Call, m_ClientAccess.GetRoom());
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
            return objects.Select(
                              obj => ArgumentFactory.Create(
                                  m_ClientAccess.GetStore(),
                                  obj,
                                  bTransferByValue))
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
