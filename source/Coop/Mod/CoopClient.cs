using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Common;
using Coop.Mod.Managers;
using Coop.Mod.Persistence;
using Coop.Multiplayer.Network;
using JetBrains.Annotations;
using Network.Infrastructure;
using NLog;
using RailgunNet.Logic;
using StoryMode;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using System.Threading.Tasks;

namespace Coop.Mod
{
    public class CoopClient : IUpdateable
    {
        private const int MaxReconnectAttempts = 2;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<CoopClient> m_Instance =
            new Lazy<CoopClient>(() => new CoopClient());

        [NotNull] private readonly LiteNetManagerClient m_NetManager;
        private int m_ReconnectAttempts = MaxReconnectAttempts;
        private ClientCharacterCreatorManager gameManager;

        public CoopClient()
        {
            Session = new GameSession(new GameData());
            Session.OnConnectionDestroyed += ConnectionDestroyed;
            m_NetManager = new LiteNetManagerClient(Session);
            GameState = new CoopGameState();
            Events = new CoopEvents();
            Init();
        }

        [CanBeNull] public PersistenceClient Persistence { get; private set; }

        [NotNull] public GameSession Session { get; }

        public static CoopClient Instance => m_Instance.Value;

        public CoopGameState GameState { get; }
        public CoopEvents Events { get; }

        public bool ClientPlaying
        {
            get
            {
                if (Session.Connection == null)
                {
                    return false;
                }

                // TODO change to main menu state
                return Session.Connection.State == EConnectionState.ClientPlaying;
            }
        }

        public bool ClientRequestingWorldData
        {
            get
            {
                if (Session.Connection == null)
                {
                    return false;
                }

                // TODO change to main menu state
                return Session.Connection.State == EConnectionState.ClientJoinRequesting;
            }
        }

        public void Update(TimeSpan frameTime)
        {
            m_NetManager.Update(frameTime);
            Persistence?.Update(frameTime);
        }

        public void Connect(IPAddress ip, int iPort)
        {
            m_NetManager.Connect(ip, iPort);
        }

        private void Init()
        {
            Session.OnConnectionCreated += ConnectionCreated;
            if (Session.Connection != null)
            {
                ConnectionCreated(Session.Connection);
            }
        }

        private void TryInitPersistence(ConnectionClient con)
        {
            if (con == null || con.State != EConnectionState.ClientPlaying) return;

            if (Persistence == null)
            {
                Persistence = new PersistenceClient(new GameEnvironmentClient());
            }

            Persistence.SetConnection(con);
        }

        private void ConnectionCreated(ConnectionClient con)
        {
            if (con == null)
            {
                throw new ArgumentNullException(nameof(con));
            }

            m_ReconnectAttempts = MaxReconnectAttempts;
            TryInitPersistence(con);

            #region events
            // Upward
            Session.Connection.RequireCharacterCreation += CreateCharacter;
            Session.Connection.OnCharacterCreated += CharacterCreated;

            Session.Connection.OnClientLoaded += TryInitPersistence;
            Session.Connection.OnDisconnected += ConnectionClosed;

            // Downward
            if(con.State == EConnectionState.ClientLoading)
            {
                ClientCharacterCreatorManager.OnLoadFinishedEvent += Session.Connection.sendGameLoaded;
            }
            #endregion

        }

        private void CreateCharacter(ConnectionClient con)
        {
            if (gameManager == null)
            {
                gameManager = new ClientCharacterCreatorManager();
                MBGameManager.StartNewGame(gameManager);
                ClientCharacterCreatorManager.OnLoadFinishedEvent += (object source, EventArgs e) =>
                {
                    StoryModeEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, () =>
                    {
                        CharacterCreated(con);
                    });
                };
            }
        }

        private void CharacterCreated(ConnectionClient con)
        {
            // Switch state
        }

        private void ConnectionClosed(EDisconnectReason eReason)
        {
            Persistence?.SetConnection(null);
        }

        private void ConnectionDestroyed(EDisconnectReason eReason)
        {
            switch (eReason)
            {
                case EDisconnectReason.Timeout:
                case EDisconnectReason.Unknown:
                    TryReconnect();
                    break;
            }
        }

        private void TryReconnect()
        {
            if (m_ReconnectAttempts > 0)
            {
                Logger.Info(
                    "Reconnect attempt [{currentAttempt}/{max}].",
                    m_ReconnectAttempts,
                    MaxReconnectAttempts);
                --m_ReconnectAttempts;
                m_NetManager.Reconnect();
            }
        }

        public override string ToString()
        {
            if (Session.Connection == null)
            {
                return "Client not connected.";
            }

            string sLeadingWhitespace = "       ";
            string sRet =
                $"{Session.Connection.Latency,-5}{Session.Connection.State,-30}{Session.Connection.Network}";
            sRet += Environment.NewLine + sLeadingWhitespace;
            if (Persistence != null)
            {
                IEnumerable<RailEntityBase> controlledEntity = Persistence.Room.LocalEntities;
                sRet += $"Controlling {controlledEntity.Count()} entities.";
                foreach (RailEntityBase entity in controlledEntity)
                {
                    sRet += Environment.NewLine + sLeadingWhitespace + entity;
                }
            }

            return sRet;
        }
    }
}
