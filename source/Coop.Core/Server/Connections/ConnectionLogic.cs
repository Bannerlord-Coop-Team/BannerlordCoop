using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.States;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Connections
{
    public interface IConnectionLogic : IConnectionState
    {
        NetPeer Peer { get; }
        string HeroStringId { get; set; }
        IMessageBroker MessageBroker { get; }
        INetwork Network { get; }
        IConnectionState State { get; set; }    
    }

    public class ConnectionLogic : IConnectionLogic
    {
        private readonly ILogger Logger = LogManager.GetLogger<ConnectionLogic>();

        public NetPeer Peer { get; }
        public string HeroStringId { get; set; }

        public IMessageBroker MessageBroker { get; }
        public INetwork Network { get; }

        public IConnectionState State 
        {
            get { return _state; }
            set
            {
                Logger.Debug("Connection is changing to {state} State", value.GetType().Name);
                _state?.Dispose();
                _state = value;
            }
        }        

        private IConnectionState _state;

        public ConnectionLogic(NetPeer playerId, IMessageBroker messageBroker, INetwork network)
        {
            Peer = playerId;
            MessageBroker = messageBroker;
            Network = network;
            State = new ResolveCharacterState(this);
        }

        public void Dispose() => State.Dispose();

        public void CreateCharacter() => State.CreateCharacter();

        public void TransferSave() => State.TransferSave();

        public void Load() => State.Load();

        public void EnterCampaign() => State.EnterCampaign();

        public void EnterMission() => State.EnterMission();
    }
}
