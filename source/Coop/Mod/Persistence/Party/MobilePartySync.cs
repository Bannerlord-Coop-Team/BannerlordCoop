using System;
using System.Collections.Generic;
using System.Reflection;
using CoopFramework;
using NLog;
using Sync;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Logger = NLog.Logger;

namespace Coop.Mod.Persistence.Party
{
    public class MobilePartySync : SynchronizationBase
    {
        public override void Broadcast(MethodId id, object instance, object[] args)
        {
            // We didn't patch any methods, so this is never called.
            throw new System.NotImplementedException();
        }
        public override void Broadcast(FieldChangeBuffer buffer)
        {
            var changes = SortByParty(buffer.FetchChanges());
            foreach (var change in changes)
            {
                MobileParty party = change.Key;
                if(!m_Handlers.TryGetValue(party.Id, out IMovementHandler handler))
                {
                    Logger.Warn("Got FieldChangeBuffer for unmanaged {party}. Ignored.");
                    continue;
                }

                MovementData latest = handler.GetLatest();
                UpdateData(latest, change.Value);
                handler.RequestMovement(latest);
            }
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
        
        #region Private

        private Dictionary<MobileParty, List<Tuple<FieldInfo, ValueChangeRequest>>> SortByParty(
            Dictionary<FieldAccess, Dictionary<object, ValueChangeRequest>> input)
        {
            Dictionary<MobileParty, List<Tuple<FieldInfo, ValueChangeRequest>>> requestedChanges = new Dictionary<MobileParty, List<Tuple<FieldInfo, ValueChangeRequest>>>();
            foreach (var bufferEntry in input)
            {
                FieldAccess field = bufferEntry.Key;
                if (field.MemberInfo.DeclaringType != typeof(MobileParty))
                {
                    continue;
                }

                foreach (var change in bufferEntry.Value)
                {
                    if (change.Key is MobileParty party)
                    {
                        if (!requestedChanges.ContainsKey(party))
                        {
                            requestedChanges[party] = new List<Tuple<FieldInfo, ValueChangeRequest>>();
                        }

                        requestedChanges[party]
                            .Add(new Tuple<FieldInfo, ValueChangeRequest>(field.MemberInfo, change.Value));
                    }
                }
            }

            return requestedChanges;
        }
        
        private void UpdateData(MovementData toUpdate, List<Tuple<FieldInfo, ValueChangeRequest>> changes)
        {
            foreach (var change in changes)
            {
                FieldInfo field = change.Item1;
                if (field.FieldType == typeof(AiBehavior))
                {
                    toUpdate.DefaultBehaviour = (AiBehavior) change.Item2.RequestedValue;
                }
                else if (field.FieldType == typeof(Settlement))
                {
                    toUpdate.TargetSettlement = (Settlement) change.Item2.RequestedValue;
                }
                else if (field.FieldType == typeof(MobileParty))
                {
                    toUpdate.TargetParty = (MobileParty) change.Item2.RequestedValue;
                }
                else if (field.FieldType == typeof(Vec2))
                {
                    toUpdate.TargetPosition = (Vec2) change.Item2.RequestedValue;
                }
                else if (field.FieldType == typeof(int))
                {
                    toUpdate.NumberOfFleeingsAtLastTravel = (int) change.Item2.RequestedValue;
                }
            }
        }

        private readonly Dictionary<MBGUID, IMovementHandler> m_Handlers =
            new Dictionary<MBGUID, IMovementHandler>();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        #endregion
    }
}