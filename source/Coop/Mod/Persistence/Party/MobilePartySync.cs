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
    public class MobilePartySync : SynchronizationBase
    {
        public MobilePartySync([NotNull] FieldAccessGroup<MobileParty, MovementData> movement)
        {
            m_MovementGroup = movement ?? throw new ArgumentNullException();
        }
        public override void Broadcast(MethodId id, object instance, object[] args)
        {
            // We didn't patch any methods, so this is never called.
            throw new System.NotImplementedException();
        }

        public void Register(MobileParty party, IMovementHandler handler)
        {
            if (m_Handlers.ContainsKey(party.Id))
            {
                Logger.Warn("Duplicate entity register for {party}.", party);
            }

            m_Handlers[party.Id] = handler;
        }

        public void Unregister(IMovementHandler railEntity)
        {
            foreach (var keyVal in m_Handlers)
            {
                if (keyVal.Value != railEntity) continue;
                
                m_Handlers.Remove(keyVal.Key); // Attention: invalidates iterator!
                return;
            }
        }
        
        public void SetAuthoritative(MobileParty party, MovementData data, bool bIsPlayerControlled)
        {
            if (!data.IsValid())
            {
#if DEBUG
                throw new InvalidOperationException();
#else
                Logger.Warn("Received invalid data for {Party}: {Movement}. Ignored", party, data);
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


        public override void BroadcastBufferedChanges(FieldChangeBuffer buffer)
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