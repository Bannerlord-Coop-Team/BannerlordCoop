using System.Collections.Generic;
using System.Linq;
using CoopFramework;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RemoteAction;
using Sync.Behaviour;
using Sync.Call;
using Sync.Store;
using Sync.Value;

namespace Coop.Mod.Persistence.RemoteAction
{
    /// <summary>
    ///     Default synchronization implementation for remote procedure calls (RPC) on clients.
    ///
    ///     Uses the <see cref="ArgumentFactory"/> to serialize & broadcast all arguments as well as the instance.
    ///     After the instance & arguments have been received by all clients, the call is invoked on the next
    ///     game tick.
    /// </summary>
    public class CoopSyncServer : SyncBuffered
    {
        /// <inheritdoc cref="ISynchronization.Broadcast(InvokableId, object, object[])"/>
        public override void Broadcast([CanBeNull] EntityId[] affectedEntities, InvokableId id, object instance, object[] args)
        {
            RailServerRoom room = CoopServer.Instance?.Persistence?.Room;
            SharedRemoteStore store = CoopServer.Instance?.SyncedObjectStore;
            EventBroadcastingQueue queue = CoopServer.Instance?.Persistence?.EventQueue;
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
            
            EventMethodCall evt = room.CreateEvent<EventMethodCall>();
            evt.Call = call;
            evt.Entities = affectedEntities;
            BroadcastHistory.Push(evt.Call, room.Tick);
            queue.Add(room, evt);
        }
        /// <inheritdoc cref="SyncBuffered.BroadcastBufferedChanges(FieldChangeBuffer)"/>
        protected override void BroadcastBufferedChanges(FieldChangeBuffer buffer)
        {
            RailServerRoom room = CoopServer.Instance?.Persistence?.Room;
            SharedRemoteStore store = CoopServer.Instance?.SyncedObjectStore;
            EventBroadcastingQueue queue = CoopServer.Instance?.Persistence?.EventQueue;
            
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
                    EventFieldChange evt = room.CreateEvent<EventFieldChange>();
                    evt.Field = fieldChange;
                    BroadcastHistory.Push(evt.Field, room.Tick);
                    queue.Add(room, evt);
                }
            }
        }
        #region Private

        private List<Argument> ProduceArguments(IStore store, EInvokableFlag flags, object[] args)
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
        #endregion
    }
}