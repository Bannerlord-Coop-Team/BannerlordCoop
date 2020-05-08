using System;
using Coop.Common;
using Coop.Game.Patch;
using Coop.Game.Persistence.Party;
using Coop.Multiplayer;
using Coop.Multiplayer.Network;
using JetBrains.Annotations;
using RailgunNet;
using RailgunNet.Connection.Client;

namespace Coop.Game.Persistence
{
    public class PersistenceClient : IUpdateable
    {
        private readonly EntityMapping m_Mapping;
        private readonly RailClient m_RailClient;
        private readonly RailClientRoom m_Room;

        public PersistenceClient(IEnvironment environment)
        {
            m_Mapping = new EntityMapping();
            m_RailClient = new RailClient(Registry.Get(Component.Client, environment, m_Mapping));
            CampaignMapMovement.s_Environment = environment;
            m_Room = m_RailClient.StartRoom();
        }

        public void Update(TimeSpan frameTime)
        {
            m_RailClient.Update();
        }

        public void SetConnection([CanBeNull] ConnectionClient connection)
        {
            m_RailClient.SetPeer((RailNetPeerWrapper) connection?.GameStatePersistence);
        }
    }
}
