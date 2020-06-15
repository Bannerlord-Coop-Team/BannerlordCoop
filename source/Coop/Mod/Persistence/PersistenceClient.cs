using System;
using System.Collections.Generic;
using Common;
using Coop.Mod.Persistence.RPC;
using Coop.NetImpl.LiteNet;
using JetBrains.Annotations;
using Network.Infrastructure;
using RailgunNet.Connection.Client;
using Sync;

namespace Coop.Mod.Persistence
{
    public class PersistenceClient : IUpdateable
    {
        private readonly IEnvironmentClient m_Environment;
        [NotNull] private readonly RailClient m_RailClient;
        [CanBeNull] public RailClientPeer Peer => m_RailClient.ServerPeer;

        public PersistenceClient(IEnvironmentClient environment)
        {
            m_Environment = environment;
            m_RailClient = new RailClient(Registry.Client(environment));
            Room = m_RailClient.StartRoom();
            RpcSyncHandlers = new RPCSyncHandlers();
        }

        [NotNull] public RPCSyncHandlers RpcSyncHandlers { get; }

        [NotNull] public RailClientRoom Room { get; }

        public void Update(TimeSpan frameTime)
        {
            List<object> toRemove = new List<object>();
            foreach (KeyValuePair<ValueAccess, Dictionary<object, ValueChangeRequest>> buffer in
                FieldChangeBuffer.BufferedChanges)
            {
                ValueAccess access = buffer.Key;
                foreach (KeyValuePair<object, ValueChangeRequest> instanceBuffer in buffer.Value)
                {
                    object instance = instanceBuffer.Key;
                    if (CheckShouldRemove(access, instance, instanceBuffer.Value))
                    {
                        toRemove.Add(instanceBuffer.Key);
                    }
                    else if (!instanceBuffer.Value.RequestProcessed)
                    {
                        access.GetHandler(instance)?.Invoke(instanceBuffer.Value.RequestedValue);
                        instanceBuffer.Value.RequestProcessed = true;
                    }
                }

                toRemove.ForEach(o => buffer.Value.Remove(o));
                toRemove.Clear();
            }

            m_RailClient.Update();
        }

        public void SetConnection([CanBeNull] ConnectionClient connection)
        {
            m_RailClient.SetPeer((RailNetPeerWrapper) connection?.GameStatePersistence);
        }

        private bool CheckShouldRemove(
            ValueAccess access,
            object instance,
            ValueChangeRequest buffer)
        {
            if (buffer.RequestProcessed && Equals(buffer.RequestedValue, buffer.LatestActualValue))
            {
                return true;
            }

            object currentValue = access.Get(instance);
            if (Equals(currentValue, buffer.LatestActualValue))
            {
                return false;
            }

            if (buffer.RequestProcessed)
            {
                return true;
            }

            buffer.LatestActualValue = currentValue;
            return false;
        }
    }
}
