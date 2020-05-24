using System;
using System.Collections.Generic;
using Common;
using Coop.Multiplayer.Network;
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

        public PersistenceClient(IEnvironmentClient environment)
        {
            m_Environment = environment;
            m_RailClient = new RailClient(Registry.Client(environment));
            Room = m_RailClient.StartRoom();
        }

        [NotNull] public RailClientRoom Room { get; }

        public void Update(TimeSpan frameTime)
        {
            List<object> toRemove = new List<object>();
            foreach (KeyValuePair<SyncValue, Dictionary<object, BufferedData>> fieldBuffer in
                FieldWatcher.BufferedChanges)
            {
                SyncValue syncable = fieldBuffer.Key;
                foreach (KeyValuePair<object, BufferedData> instanceBuffer in fieldBuffer.Value)
                {
                    object instance = instanceBuffer.Key;
                    if (CheckShouldRemove(syncable, instance, instanceBuffer.Value))
                    {
                        toRemove.Add(instanceBuffer.Key);
                    }
                    else if (!instanceBuffer.Value.Sent)
                    {
                        SyncValue field = fieldBuffer.Key;
                        field.GetSyncHandler(instance)?.Invoke(instanceBuffer.Value.ToSend);
                        field.GetSyncHandler(SyncableInstance.Any)
                             ?.Invoke(instanceBuffer.Value.ToSend);
                        instanceBuffer.Value.Sent = true;
                    }
                }

                toRemove.ForEach(o => fieldBuffer.Value.Remove(o));
                toRemove.Clear();
            }

            m_RailClient.Update();
        }

        public void SetConnection([CanBeNull] ConnectionClient connection)
        {
            m_RailClient.SetPeer((RailNetPeerWrapper) connection?.GameStatePersistence);
        }

        private bool CheckShouldRemove(SyncValue syncable, object instance, BufferedData buffer)
        {
            if (buffer.Sent && Equals(buffer.ToSend, buffer.Actual))
            {
                return true;
            }

            object currentValue = syncable.Get(instance);
            if (Equals(currentValue, buffer.Actual))
            {
                return false;
            }

            if (buffer.Sent)
            {
                return true;
            }

            buffer.Actual = currentValue;
            return false;
        }
    }
}
