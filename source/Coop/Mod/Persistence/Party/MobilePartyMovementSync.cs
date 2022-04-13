using System;
using System.Collections.Generic;
using Common;
using Coop.Mod.GameSync;
using CoopFramework;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RailgunNet.System.Types;
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
    ///     Synchronization implementation for position & movement data of a <see cref="MobileParty"/>. It records 
    ///     buffered changes to 2 different field groups:
    ///     1.  The captured <see cref="MovementData" />.
    ///     2.  The captured 2d position on the campaign map.
    ///     These changes are forwarded to the <see cref="IMovementHandler" />. The following handlers are registered:
    ///     -   On every client: <see cref="MobilePartyEntityClient" /> for every party that is directly controlled by
    ///     this local client, i.e. its main party.
    ///     -   On the server: <see cref="MobilePartyEntityServer" /> for every party that is not directly controlled by
    ///     any client.
    /// </summary>
    public class MobilePartyMovementSync : SyncBuffered
    {
        public MobilePartyMovementSync(
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
        /// <exception cref="NotImplementedException"></exception>
        public override void Broadcast([CanBeNull] EntityId[] affectedEntities, InvokableId id, object instance, object[] args)
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
            Guid guid = CoopObjectManager.GetGuid(party);
            if (m_Handlers.ContainsKey(guid))
            {
                Logger.Warn("Duplicate entity register for {party}.", party);
            }

            m_Handlers[guid] = handler;
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

                Guid guid = CoopObjectManager.GetGuid(party);
                if (!m_Handlers.TryGetValue(guid, out IMovementHandler handler))
                {
                    Logger.Debug("Got FieldChangeBuffer for unmanaged {party}. Ignored.", party);
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

                Guid guid = CoopObjectManager.GetGuid(party);
                if (!m_Handlers.TryGetValue(guid, out IMovementHandler handler))
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
        private readonly Dictionary<Guid, IMovementHandler> m_Handlers =
            new Dictionary<Guid, IMovementHandler>();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly FieldAccessGroup<MobileParty, MovementData> m_MovementOrder;
        [NotNull] private readonly FieldAccess<MobileParty, Vec2> m_MapPosition;

        #endregion
    }
}