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
        public enum EState
        {
            Inactive,
            Starting,
            Running,
            Stopping
        }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public readonly UpdateableList Updateables;
        public ServerConfiguration ActiveConfig;
        public EState State => m_State.State;

        public bool AreAllClientsPlaying =>
            ActiveConnections.All(con => con.State == EConnectionState.ServerPlaying);

        public event Action<ConnectionServer> OnClientConnected;

        public void Start(ServerConfiguration config)
        {
            if (m_State.IsInState(EState.Inactive))
            {
                m_State.Fire(
                    new StateMachine<EState, ETrigger>.TriggerWithParameters<ServerConfiguration>(
                        ETrigger.Start),
                    config);
            }
        }

        public void Stop()
        {
            if (State != EState.Inactive)
            {
                m_State.Fire(ETrigger.Stop);
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
                $"LAN:   {ActiveConfig.LanAddress}:{ActiveConfig.LanPort}",
                $"WAN:   {ActiveConfig.WanAddress}:{ActiveConfig.WanPort}");

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
        }

        public virtual bool CanPlayerJoin()
        {
            return State == EState.Running && ActiveConnections.Count < ActiveConfig.MaxPlayerCount;
        }

        #region internals
        private enum ETrigger
        {
            Start,
            Initialized,
            Stop,
            Stopped
        }

        public enum EType
        {
            Threaded,
            Direct
        }

        private readonly EType m_ServerType;

        public Server(EType eType)
        {
            m_ServerType = eType;
            Updateables = new UpdateableList();
            m_State = new StateMachine<EState, ETrigger>(EState.Inactive);

            m_State.Configure(EState.Inactive).Permit(ETrigger.Start, EState.Starting);

            StateMachine<EState, ETrigger>.TriggerWithParameters<ServerConfiguration> startTrigger =
                m_State.SetTriggerParameters<ServerConfiguration>(ETrigger.Start);
            m_State.Configure(EState.Starting)
                   .OnEntryFrom(startTrigger, Load)
                   .Permit(ETrigger.Initialized, EState.Running)
                   .Permit(ETrigger.Stop, EState.Stopping);

            m_State.Configure(EState.Running)
                   .OnEntryFrom(ETrigger.Initialized, StartMainLoop)
                   .OnExit(StopMainLoop)
                   .Permit(ETrigger.Stop, EState.Stopping);

            m_State.Configure(EState.Stopping)
                   .OnEntry(ShutDown)
                   .Permit(ETrigger.Stopped, EState.Inactive);
        }

        ~Server()
        {
            Stop();
        }

        public List<ConnectionServer> ActiveConnections { get; } = new List<ConnectionServer>();

        private void Load(ServerConfiguration config)
        {
            ActiveConfig = config;
            m_State.Fire(ETrigger.Initialized);
        }

        private void ShutDown()
        {
            ActiveConfig = null;
            foreach (ConnectionServer conn in ActiveConnections)
            {
                conn.Disconnect(EDisconnectReason.ServerShutDown);
            }

            ActiveConnections.Clear();
            m_State.Fire(ETrigger.Stopped);
        }

        private readonly StateMachine<EState, ETrigger> m_State;
        private bool m_IsStopRequest;
        private readonly object m_StopRequestLock = new object();
        private Thread m_Thread;
        [CanBeNull] private FrameLimiter m_FrameLimiter;

        public TimeSpan AverageFrameTime => m_FrameLimiter?.AverageFrameTime ?? TimeSpan.Zero;

        private void StartMainLoop()
        {
            if (m_ServerType == EType.Threaded)
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
