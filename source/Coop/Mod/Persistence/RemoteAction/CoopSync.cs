using System;
using System.Collections.Generic;
using System.Linq;
using CoopFramework;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Connection.Client;
using RemoteAction;
using Sync;
using Sync.Behaviour;
using Sync.Call;
using Sync.Store;
using Sync.Value;

namespace Coop.Mod.Persistence.RemoteAction
{
    /// <summary>
    ///     Default synchronization implementation for remote procedure calls (RPC.
    ///
    ///     Uses the <see cref="ArgumentFactory"/> to serialize & broadcast all arguments as well as the instance.
    ///     After the instance & arguments have been received by all clients, the call is invoked on the next
    ///     game tick.
    /// </summary>
    public class CoopSync : SyncBuffered
    {
        public CoopSync([NotNull] IClientAccess access)
        {
            m_ClientAccess = access;
        }
        /// <inheritdoc cref="ISynchronization.Broadcast(InvokableId, object, object[])"/>
        public override void Broadcast(InvokableId id, object instance, object[] args)
        {
            RemoteStore store = m_ClientAccess.GetStore();
            RailClientRoom room = m_ClientAccess.GetRoom();
            if (store == null)
            {
                Logger.Error("RemoteStore is null. Cannot broadcast {Call}", Sync.Registry.IdToInvokable[id]);
                return;
            }
            if (room == null)
            {
                Logger.Error("RailRoom is null. Cannot broadcast {Call}", Sync.Registry.IdToInvokable[id]);
                return;
            }
            
            var invokable = Sync.Registry.IdToInvokable[id];
            var call = new MethodCall(
                id,
                ArgumentFactory.Create(
                    store,
                    instance,
                    false),
            ProduceArguments(store, invokable.Flags, args));
            room.RaiseEvent<EventMethodCall>(
                    evt =>
                    {
                        evt.Call = call;
                        BroadcastHistory.Push(evt.Call, room.Tick);
                    });
        }
        /// <inheritdoc cref="SyncBuffered.BroadcastBufferedChanges(FieldChangeBuffer)"/>
        protected override void BroadcastBufferedChanges(FieldChangeBuffer buffer)
        {
            RemoteStore store = m_ClientAccess.GetStore();
            RailClientRoom room = m_ClientAccess.GetRoom();
            
            foreach (var change in buffer.FetchChanges())
            {
                if (store == null)
                {
                    Logger.Error("RemoteStore is null. Cannot broadcast {ValueChange}", change.Key);
                    return;
                }
                if (room == null)
                {
                    Logger.Error("RailRoom is null. Cannot broadcast {FieldChange}");
                    return;
                }
                
                FieldBase field = change.Key;
                foreach (var instanceChange in change.Value)
                {
                    var argInstance = ArgumentFactory.Create(
                        store,
                        instanceChange.Key,
                        false);
                    var argValue = ArgumentFactory.Create(
                        store,
                        instanceChange.Value.RequestedValue,
                        false);
                    var fieldChange = new FieldChange(
                        field.Id,
                        argInstance,
                        argValue);
                    room.RaiseEvent<EventFieldChange>(
                            evt =>
                            {
                                evt.Field = fieldChange;
                                BroadcastHistory.Push(evt.Field, m_ClientAccess.GetRoom().Tick);
                            });
                }
            }
        }
        #region Private

        private List<Argument> ProduceArguments(RemoteStore store, EInvokableFlag flags, object[] args)
        {
            var bTransferByValue = flags.HasFlag(EInvokableFlag.TransferArgumentsByValue);
            return args.Select(
                    obj => ArgumentFactory.Create(
                        store,
                        obj,
                        bTransferByValue))
                .ToList();
        }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly IClientAccess m_ClientAccess;

        #endregion
    }
}