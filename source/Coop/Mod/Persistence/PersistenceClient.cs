using System;
using System.Collections.Generic;
using Common;
using Coop.Mod.Persistence.RemoteAction;
using Coop.NetImpl.LiteNet;
using CoopFramework;
using JetBrains.Annotations;
using Network.Infrastructure;
using RailgunNet.Connection.Client;
using RemoteAction;
using Sync;
using Sync.Behaviour;
using Sync.Value;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Manages the <see cref="FieldChangeStack" />, that is it calls the responsible handlers
    ///     for all requested changes once per <see cref="Update" />. As the server is currently
    ///     running in the host client, this class is also responsible for triggering the serverside
    ///     field updates!
    /// </summary>
    public class PersistenceClient : IUpdateable
    {
        [NotNull] private readonly RailClient m_RailClient;

        public PersistenceClient(IEnvironmentClient environment)
        {
            Environment = environment;
            m_RailClient = new RailClient(Registry.Client(Environment));
            Room = m_RailClient.StartRoom();
        }

        public IEnvironmentClient Environment { get; }
        [CanBeNull] public RailClientPeer Peer => m_RailClient.ServerPeer;

        [NotNull] public RailClientRoom Room { get; }

        public void Update(TimeSpan frameTime)
        {
            m_RailClient.Update();
        }
        public int Priority { get; } = UpdatePriority.MainLoop.RailGun;

        public void SetConnection([CanBeNull] ConnectionClient connection)
        {
            m_RailClient.SetPeer((RailNetPeerWrapper) connection?.GameStatePersistence);
        }
    }
}
