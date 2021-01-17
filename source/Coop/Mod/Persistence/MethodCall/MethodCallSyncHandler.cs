using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Common;
using JetBrains.Annotations;
using NLog;
using RailgunNet;
using RailgunNet.Connection.Client;
using RailgunNet.System.Types;
using RailgunNet.Util;
using RemoteAction;
using Sync;

namespace Coop.Mod.Persistence.MethodCall
{
    /// <summary>
    ///     Registers a global call handler for a <see cref="MethodAccess" /> that sends an
    ///     <see cref="EventMethodCall" /> to the server.
    /// </summary>
    [OnlyIn(Component.Client)]
    public class MethodCallSyncHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
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
        private void Trace(RemoteAction.MethodCall call, RailClientRoom room)
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
                        bool bDebounce =
                            MethodAccess.Flags.HasFlag(EMethodPatchFlag.DebounceCalls);
                        RemoteAction.MethodCall call = new RemoteAction.MethodCall(
                            MethodAccess.Id,
                            ArgumentFactory.Create(
                                m_ClientAccess.GetStore(),
                                instance,
                                false),
                            ProduceArguments(objects));

                        if (bDebounce && PendingRequests.Instance.IsPending(call))
                        {
                            Logger.Debug("Debounced RPC {}", call);
                        }
                        else
                        {
                            PendingRequests.Instance.Add(call);
                            m_ClientAccess.GetRoom()
                                          ?.RaiseEvent<EventMethodCall>(
                                              evt =>
                                              {
                                                  evt.Call = call;
                                                  Trace(evt.Call, m_ClientAccess.GetRoom());
                                              });
                        }
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
                public RemoteAction.MethodCall Call { get; set; }
                public Tick Tick { get; set; }
            }
        }
    }
}
