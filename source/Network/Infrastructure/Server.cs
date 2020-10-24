using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common;
using JetBrains.Annotations;
using Network.Protocol;
using NLog;
using Stateless;

namespace Network.Infrastructure
{
    public class Server : IUpdateable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public readonly UpdateableList Updateables;
        public ServerConfiguration ActiveConfig;
        public Enum State => m_ServerSM.StateMachine.State;

        public event Action<ConnectionServer> OnClientConnected;
        public event Action<ConnectionServer, EDisconnectReason> OnClientDisconnected;

        public void Start(ServerConfiguration config)
        {
            if (m_ServerSM.StateMachine.IsInState(EServerState.Inactive))
            {
                m_ServerSM.StateMachine.Fire(
                    new StateMachine<EServerState, EServerTrigger>.TriggerWithParameters<ServerConfiguration>(
                        EServerTrigger.Start),
                    config);
            }
        }

        public void Stop()
        {
            if (!State.Equals(EServerState.Inactive))
            {
                m_ServerSM.StateMachine.Fire(EServerTrigger.Stop);
            }
        }

        public void SendToAll(Packet packet)
        {
            foreach (ConnectionServer conn in ActiveConnections)
            {
                conn.Send(packet);
            }
        }

        public override string ToString()
        {
            string sDump = string.Join(
                Environment.NewLine,
                $"Server is '{State.ToString()}' with '{ActiveConnections.Count}/{ActiveConfig.MaxPlayerCount}' players.",
                $"LAN:   {ActiveConfig.NetworkConfiguration.LanAddress}:{ActiveConfig.NetworkConfiguration.LanPort}",
                $"WAN:   {ActiveConfig.NetworkConfiguration.WanAddress}:{ActiveConfig.NetworkConfiguration.WanPort}");

            if (ActiveConnections.Count > 0)
            {
                sDump += Environment.NewLine + "Connections to clients:";
                sDump += Environment.NewLine + "Ping " + "State                         Network";
                foreach (ConnectionServer conn in ActiveConnections)
                {
                    sDump += Environment.NewLine + $"{conn}";
                }
            }

            return sDump;
        }

        public virtual void Connected(ConnectionServer con)
        {
            ActiveConnections.Add(con);
            OnClientConnected?.Invoke(con);
            Logger.Info("Connection established: {connection}.", con);
        }

        public virtual void Disconnected(ConnectionServer con, EDisconnectReason eReason)
        {
            Logger.Info("Connection closed: {connection}. {reason}.", con, eReason);
            con.Disconnect(eReason);
            if (!ActiveConnections.Remove(con))
            {
                Logger.Error("Unknown connection: {connection}.", con);
            }

            OnClientDisconnected?.Invoke(con, eReason);
        }

        public virtual bool CanPlayerJoin()
        {
            return State.Equals(EServerState.Running) && ActiveConnections.Count < ActiveConfig.MaxPlayerCount;
        }

        #region internals
       

        public enum EType
        {
            Threaded,
            Direct
        }

        public EType ServerType { get; }

        public Server(EType eType)
        {
            ServerType = eType;
            Updateables = new UpdateableList();
            m_ServerSM = new ServerSM();

            #region State Machine Configuration
            m_ServerSM.StartingState.OnEntryFrom(m_ServerSM.StartTrigger, Load);

            m_ServerSM.RunningState
                .OnEntryFrom(EServerTrigger.Initialized, StartMainLoop)
                .OnExit(StopMainLoop);

            m_ServerSM.StoppingState.OnEntry(ShutDown);
            #endregion
        }

        ~Server()
        {
            Stop();
        }

        public List<ConnectionServer> ActiveConnections { get; } = new List<ConnectionServer>();

        private void Load(ServerConfiguration config)
        {
            ActiveConfig = config;
            m_ServerSM.StateMachine.Fire(EServerTrigger.Initialized);
        }

        private void ShutDown()
        {
            ActiveConfig = null;
            foreach (ConnectionServer conn in ActiveConnections)
            {
                conn.Disconnect(EDisconnectReason.ServerShutDown);
            }

            ActiveConnections.Clear();
            m_ServerSM.StateMachine.Fire(EServerTrigger.Stopped);
        }

        private readonly ServerSM m_ServerSM;
        private bool m_IsStopRequest;
        private readonly object m_StopRequestLock = new object();
        private Thread m_Thread;
        [CanBeNull] private FrameLimiter m_FrameLimiter;

        public TimeSpan AverageFrameTime => m_FrameLimiter?.AverageFrameTime ?? TimeSpan.Zero;

        private void StartMainLoop()
        {
            if (ServerType == EType.Threaded)
            {
                m_Thread = new Thread(Run);
                lock (m_StopRequestLock)
                {
                    m_IsStopRequest = false;
                }

                m_Thread.Start();
            }
        }

        private void Run()
        {
            m_FrameLimiter = new FrameLimiter(
                ActiveConfig.TickRate > 0 ?
                    TimeSpan.FromMilliseconds(1000 / (double) ActiveConfig.TickRate) :
                    TimeSpan.Zero);
            bool bRunning = true;
            while (bRunning)
            {
                Update(m_FrameLimiter.LastFrameTime);
                m_FrameLimiter.Throttle();
                lock (m_StopRequestLock)
                {
                    bRunning = !m_IsStopRequest;
                }
            }

            m_FrameLimiter = null;
        }

        private void StopMainLoop()
        {
            if (m_Thread == null)
            {
                return;
            }

            lock (m_StopRequestLock)
            {
                m_IsStopRequest = true;
            }

            m_Thread.Join();
            m_Thread = null;
        }

        public void Update(TimeSpan frameTime)
        {
            Updateables.UpdateAll(frameTime);
        }
        #endregion
    }
}
