using System;
using System.Collections.Generic;
using System.Reflection;
using CoopFramework;
using JetBrains.Annotations;
using NLog;
using RemoteAction;
using Sync;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using Logger = NLog.Logger;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Synchronization implementation for the map movement data of <see cref="MobileParty"/>.
    /// </summary>
    public class MobilePartySync : SyncBuffered
    {
        public MobilePartySync([NotNull] FieldAccessGroup<MobileParty, MovementData> movement)
        {
            m_MovementGroup = movement ?? throw new ArgumentNullException();
        }
        /// <inheritdoc cref="ISynchronization.Broadcast(MethodId, object, object[])"/>
        public override void Broadcast(MethodId id, object instance, object[] args)
        {
            // We didn't patch any methods, so this is never called.
            throw new System.NotImplementedException();
        }
        /// <summary>
        ///     Register a handler to be called when the movement data of a <see cref="MobileParty"/> is changed locally
        ///     by the regular game loop.
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
                if (keyVal.Value != railEntity) continue;
                
                m_Handlers.Remove(keyVal.Key); // Attention: invalidates iterator!
                return;
            }
        }
        /// <summary>
        ///     Set the movement data of a <see cref="MobileParty"/> as an authoritative action originating from the
        ///     server.
        /// </summary>
        /// <param name="party">Instance to set the data on.</param>
        /// <param name="data">Data to set.</param>
        /// <param name="bIsPlayerControlled">`true` if the party is controlled by any player, local or remote.</param>
        /// <exception cref="InvalidOperationException">When <paramref name="data"/> is inconsistent.</exception>
        public void SetAuthoritative(MobileParty party, MovementData data, bool bIsPlayerControlled)
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
            
            m_MovementGroup.SetTyped(party, data);
            if (bIsPlayerControlled && !Coop.IsController(party))
            {
                // That is a remote player moving. We need to update the local MainParty as well
                // because Campaign.Tick will otherwise not update the AI decisions and just
                // ignore some actions (for example EngageParty).
                SetDefaultBehaviourNeedsUpdate(Campaign.Current.MainParty);
            }
            SetDefaultBehaviourNeedsUpdate(party);
        }
        /// <inheritdoc cref="SyncBuffered.BroadcastBufferedChanges(FieldChangeBuffer)"/>
        protected override void BroadcastBufferedChanges(FieldChangeBuffer buffer)
        {
            var changes = SortByParty(buffer.FetchChanges());
            foreach (var change in changes)
            {
                MobileParty party = change.Key;
                if(!m_Handlers.TryGetValue(party.Id, out IMovementHandler handler))
                {
                    Logger.Debug("Got FieldChangeBuffer for unmanaged {party}. Ignored.", party);
                    continue;
                }
                BroadcastHistory.Push(new CallTrace()
                {
                    Value = m_MovementGroup.Id,
                    Instance = party,
                    Arguments = new object[] {change.Value},
                    Tick = handler.Tick
                });
                
#if DEBUG
                if (!change.Value.IsValid())
                {
                    throw new InvalidOperationException();
                }
#endif
                handler.RequestMovement(change.Value);
            }
        }
        
        #region Private
        private void SetDefaultBehaviourNeedsUpdate(MobileParty party)
        {
            typeof(MobileParty).GetField(
                    "_defaultBehaviorNeedsUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(party, true);
        }

        private Dictionary<MobileParty, MovementData> SortByParty(
            Dictionary<ValueAccess, Dictionary<object, ValueChangeRequest>> input)
        {
            Dictionary<MobileParty, MovementData> requestedChanges = new Dictionary<MobileParty, MovementData>();
            foreach (var bufferEntry in input)
            {
                ValueAccess access = bufferEntry.Key;
                if (access.DeclaringType != typeof(MobileParty))
                {
                    continue;
                }

                foreach (var change in bufferEntry.Value)
                {
                    if (change.Key is MobileParty party && change.Value.RequestedValue is MovementData movementData)
                    {
                        requestedChanges[party] = movementData;
                    }
                }
            }

            return requestedChanges;
        }
        private readonly Dictionary<MBGUID, IMovementHandler> m_Handlers =
            new Dictionary<MBGUID, IMovementHandler>();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private FieldAccessGroup<MobileParty, MovementData> m_MovementGroup;

        #endregion
    }
}