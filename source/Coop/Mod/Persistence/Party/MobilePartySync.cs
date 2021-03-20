using System;
using System.Collections.Generic;
using Coop.Mod.Patch.MobilePartyPatches;
using CoopFramework;
using JetBrains.Annotations;
using NLog;
using RemoteAction;
using Sync.Call;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Logger = NLog.Logger;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Synchronization implementation for <see cref="CampaignMapMovement" />. It handles buffered changes to 2 different
    ///     field groups:
    ///     1.  The captured <see cref="MovementData" />.
    ///     2.  The captured 2d position on the campaign map.
    ///     These changes are forwarded to the responsible <see cref="IMovementHandler" />. The following handlers are
    ///     registered:
    ///     -   On every client: <see cref="MobilePartyEntityClient" /> for every party that is directly controlled by
    ///     this local client, i.e. its main party.
    ///     -   On the server: <see cref="MobilePartyEntityServer" /> for every party that is not directly controlled by
    ///     any client.
    /// </summary>
    public class MobilePartySync : SyncBuffered
    {
        /// <summary>
        ///     Invoked when the movement data of a party was changed by the server.
        /// </summary>
        public Action<MobileParty, MovementData> OnRemoteMovementChanged;
        /// <summary>
        ///     Invoker when the map position of a mobile party was changed by the server.
        /// </summary>
        public Action<MobileParty, Vec2> OnRemoteMapPostionChanged;
        public MobilePartySync(
            [NotNull] FieldAccessGroup<MobileParty, MovementData> movementOrder,
            [NotNull] FieldAccess<MobileParty, Vec2> mapPosition)
        {
            m_MovementOrder = movementOrder ?? throw new ArgumentNullException();
            m_MapPosition = mapPosition ?? throw new ArgumentNullException();
        }

        /// <summary>
        ///     No implementation provided as <see cref="CampaignMapMovement" /> does not define any method patch
        ///     that need to be synchronized.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="instance"></param>
        /// <param name="args"></param>
        /// <exception cref="NotImplementedException"></exception>
        public override void Broadcast(InvokableId id, object instance, object[] args)
        {
            // We didn't patch any methods, so this is never called.
            throw new InvalidOperationException("CampaignMapMovement was changed, but MobilePartySync not expanded.");
        }

        /// <summary>
        ///     Broadcasts the buffered changes.
        /// </summary>
        /// <param name="buffer"></param>
        protected override void BroadcastBufferedChanges(FieldChangeBuffer buffer)
        {
            var changes = buffer.FetchChanges();
            foreach (var pair in changes)
            {
                if (pair.Key.Id == m_MovementOrder.Id)
                {
                    BroadcastMovementOrder(pair.Value);
                }
                else if (pair.Key.Id == m_MapPosition.Id)
                {
                    BroadcastPosition(pair.Value);
                }
            }
        }

        /// <summary>
        ///     Register a handler to be called when the movement data of a <see cref="MobileParty" /> was changed.
        /// </summary>
        /// <param name="party">The instance the handler is for.</param>
        /// <param name="handler">The handler.</param>
        public void RegisterLocalHandler(MobileParty party, IMovementHandler handler)
        {
            if (m_Handlers.ContainsKey(party.Id))
            {
                Logger.Warn("Duplicate entity register for {party}.", party);
            }

            m_Handlers[party.Id] = handler;
        }

        /// <summary>
        ///     Removes a handler.
        /// </summary>
        /// <param name="railEntity">The handler to remove.</param>
        public void Unregister(IMovementHandler railEntity)
        {
            foreach (var keyVal in m_Handlers)
            {
                if (keyVal.Value != railEntity)
                {
                    continue;
                }

                m_Handlers.Remove(keyVal.Key); // Attention: invalidates iterator!
                return;
            }
        }

        /// <summary>
        ///     Set the movement data of a <see cref="MobileParty" /> as an authoritative action originating from the
        ///     server.
        /// </summary>
        /// <param name="party">Instance to set the data on.</param>
        /// <param name="data">Data to set.</param>
        /// <param name="bIsPlayerControlled">`true` if the party is controlled by any player, local or remote.</param>
        /// <exception cref="InvalidOperationException">When <paramref name="data" /> is inconsistent.</exception>
        public void SetAuthoritative(MobileParty party, MovementData data)
        {
            if (!data.IsValid())
            {
                string sMessage = $"Received inconsistent data for {party}: {data}. Ignored";
#if DEBUG
                throw new InvalidStateException(sMessage);
#else
                Logger.Warn(sMessage);
                return;
#endif
            }

            OnRemoteMovementChanged?.Invoke(party, data);
        }

        /// <summary>
        ///     Sets the parties 2d map position as an authoritative action.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="position"></param>
        public void SetAuthoritative(MobileParty party, Vec2 position)
        {
            OnRemoteMapPostionChanged?.Invoke(party, position);
        }

        #region Private

        /// <summary>
        ///     Broadcasts the buffered changes to <see cref="MovementData" />.
        /// </summary>
        /// <param name="changes"></param>
        /// <exception cref="Exception"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private void BroadcastMovementOrder(Dictionary<object, FieldChangeRequest> changes)
        {
            foreach (var change in changes)
            {
                MobileParty party = change.Key as MobileParty;
                if (party == null)
                {
                    throw new Exception($"{change.Key} is not a MobileParty, skip");
                }

                if (!m_Handlers.TryGetValue(party.Id, out IMovementHandler handler))
                {
                    Logger.Debug("Got FieldChangeBuffer for unmanaged {party}. Ignored.", party);
                    continue;
                }

                MovementData before = change.Value.OriginalValue as MovementData;
                if (!Coop.IsController(party))
                {
                    // Revert the local changes, we will receive the correct one from the server.
                    SetAuthoritative(party, before);
                    continue;
                }

                MovementData requested = change.Value.RequestedValue as MovementData;
                BroadcastHistory.Push(new CallTrace
                {
                    Value = m_MovementOrder.Id,
                    Instance = party,
                    Arguments = new object[] {requested},
                    Tick = handler.Tick
                });

#if DEBUG
                if (!requested.IsValid())
                {
                    throw new InvalidOperationException();
                }
#endif
                handler.RequestMovement(requested);
            }
        }

        /// <summary>
        ///     Broadcasts the buffered changes to the 2d map positions.
        /// </summary>
        /// <param name="changes"></param>
        /// <exception cref="Exception"></exception>
        private void BroadcastPosition(Dictionary<object, FieldChangeRequest> changes)
        {
            foreach (var change in changes)
            {
                MobileParty party = change.Key as MobileParty;
                if (party == null)
                {
                    throw new Exception($"{change.Key} is not a MobileParty, skip");
                }

                if (!m_Handlers.TryGetValue(party.Id, out IMovementHandler handler))
                {
                    Logger.Debug("Got FieldChangeBuffer for unmanaged {party}. Ignored.", party);
                    continue;
                }

                if (change.Value.OriginalValue is Vec2 before &&
                    change.Value.RequestedValue is Vec2 request)
                {
                    if (!Compare.CoordinatesEqual(before, request))
                    {
                        handler.RequestPosition(request);
                    }
                }
                else
                {
                    throw new Exception("Unexpected buffer content.");
                }
            }
        }
        private readonly Dictionary<MBGUID, IMovementHandler> m_Handlers =
            new Dictionary<MBGUID, IMovementHandler>();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly FieldAccessGroup<MobileParty, MovementData> m_MovementOrder;
        [NotNull] private readonly FieldAccess<MobileParty, Vec2> m_MapPosition;

        #endregion
    }
}