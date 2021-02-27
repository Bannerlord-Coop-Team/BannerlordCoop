using System;
using System.Collections.Generic;
using CoopFramework;
using HarmonyLib;
using JetBrains.Annotations;
using NLog;
using RemoteAction;
using Sync.Behaviour;
using Sync.Call;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Logger = NLog.Logger;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Synchronization implementation for the map movement data of <see cref="MobileParty"/>.
    /// </summary>
    public class MobilePartySync : SyncBuffered
    {
        public MobilePartySync(
            [NotNull] FieldAccessGroup<MobileParty, MovementData> movement, 
            [NotNull] FieldAccess<MobileParty, Vec2> mapPosition, 
            [NotNull] PatchedInvokable mapPositionSetter)
        {
            m_MovementGroup = movement ?? throw new ArgumentNullException();
            m_MapPosition = mapPosition ?? throw new ArgumentNullException();
            m_MapPositionSetter = mapPositionSetter ?? throw new ArgumentNullException();
        }
        /// <inheritdoc cref="ISynchronization.Broadcast(InvokableId, object, object[])"/>
        public override void Broadcast(InvokableId id, object instance, object[] args)
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
            
            m_MovementGroup.SetTyped(party, data);
            if (party.IsRemotePlayerMainParty())
            {
                // That is a remote player moving. We need to update the local MainParty as well
                // because Campaign.Tick will otherwise not update the AI decisions and just
                // ignore some actions (for example EngageParty).
                m_DefaultBehaviorNeedsUpdate(Campaign.Current.MainParty) = true;
            }
            else
            {
                m_DefaultBehaviorNeedsUpdate(party) = Coop.IsController(party);
            }
            
            party.RecalculateShortTermAi();
        }
        
        public void SetAuthoritative(MobileParty party, Vec2 position)
        {
            m_MapPositionSetter.Invoke(EOriginator.RemoteAuthority, party, new object[]{position});
        }
        
        /// <inheritdoc cref="SyncBuffered.BroadcastBufferedChanges(FieldChangeBuffer)"/>
        protected override void BroadcastBufferedChanges(FieldChangeBuffer buffer)
        {
            var changes = buffer.FetchChanges();
            foreach (KeyValuePair<Field,Dictionary<object,FieldChangeRequest>> pair in changes)
            {
                if (pair.Key.Id == m_MovementGroup.Id)
                {
                    BroadcastMovement(pair.Value);
                }
                else if (pair.Key.Id == m_MapPosition.Id)
                {
                    BroadcastPosition(pair.Value);
                }
            }
        }
        
        #region Private

        private void BroadcastMovement(Dictionary<object,FieldChangeRequest> changes)
        {
            foreach (var change in changes)
            {
                MobileParty party = change.Key as MobileParty;
                if (party == null)
                {
                    throw new Exception($"{change.Key} is not a MobileParty, skip");
                }
                
                if(!m_Handlers.TryGetValue(party.Id, out IMovementHandler handler))
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
                BroadcastHistory.Push(new CallTrace()
                {
                    Value = m_MovementGroup.Id,
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

        private void BroadcastPosition(Dictionary<object, FieldChangeRequest> changes)
        {
            foreach (var change in changes)
            {
                MobileParty party = change.Key as MobileParty;
                if (party == null)
                {
                    throw new Exception($"{change.Key} is not a MobileParty, skip");
                }
                
                if(!m_Handlers.TryGetValue(party.Id, out IMovementHandler handler))
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
                    throw new Exception($"Unexpected buffer content.");
                }
            }
        }
        
        private static readonly AccessTools.FieldRef<MobileParty, bool> m_DefaultBehaviorNeedsUpdate = 
            AccessTools.FieldRefAccess<MobileParty, bool>("_defaultBehaviorNeedsUpdate");

        private readonly Dictionary<MBGUID, IMovementHandler> m_Handlers =
            new Dictionary<MBGUID, IMovementHandler>();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly FieldAccessGroup<MobileParty, MovementData> m_MovementGroup;
        [NotNull] private readonly FieldAccess<MobileParty, Vec2>  m_MapPosition;
        [NotNull] private readonly PatchedInvokable m_MapPositionSetter;

        #endregion
    }
}