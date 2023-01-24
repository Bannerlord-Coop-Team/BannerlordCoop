using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Server.States;
using GameInterface;
using Serilog;
using Serilog.Core;

namespace Coop.Core.Server
{
    /// <summary>
    /// Top level server-side state machine logic orchestrator
    /// </summary>
    public interface IServerLogic : ILogic, IServerState
    {
        /// <summary>
        /// Server-side state
        /// </summary>
        IServerState State { get; set; }

        /// <summary>
        /// Networking Server for Server-side
        /// </summary>
        ICoopServer NetworkServer { get; }
    }

    /// <inheritdoc cref="IServerLogic"/>
    public class ServerLogic : IServerLogic
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopClient>();

        public IServerState State
        {
            get { return _state; }
            set
            {
                Logger.Debug("Server is changing to {state} State", value.GetType().Name);

                _state?.Dispose();
                _state = value;
            }
        }
        private IServerState _state;

        private readonly IGameInterface gameInterface;

        public IMessageBroker MessageBroker { get; }

        public ICoopServer NetworkServer { get; }

        public ServerLogic(IMessageBroker messageBroker, ICoopServer networkServer, IGameInterface gameInterface)
        {
            State = new InitialServerState(this, messageBroker);
            MessageBroker = messageBroker;
            NetworkServer = networkServer;
            this.gameInterface = gameInterface;
        }

        public void Start()
        {
            State.Start();
        }

        public void Stop()
        {
            State.Stop();
            NetworkServer.Stop();
        }

        public void Dispose()
        {
            State.Dispose();
        }
    }
}
