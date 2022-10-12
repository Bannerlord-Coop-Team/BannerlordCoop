using Common.Messaging;
using System.Collections.Generic;

namespace Coop.Core.Server.Connections.States
{
    /// <summary>
    /// Manage the state of a player's connection state
    /// </summary>
    public interface IPlayerConnectionStatesManager
    {
        /// <summary>
        /// Maps playerIds to their corresponding connection state to the primary BL2 Coop server
        /// </summary>
        IDictionary<string, IConnectionLogic> ConnectionStates { get; }

        /// <summary>
        /// Create a new connection state with a corresponding playerId as the key
        /// </summary>
        /// <param name="playerId"></param>
        void AddNewPlayer(string playerId);

        /// <summary>
        /// Remove player connection from connection mapping
        /// </summary>
        /// <param name="playerId"></param>
        void RemovePlayer(string playerId);

        /// <summary>
        /// Player has completed connection to server. Initiate loading state for client
        /// </summary>
        /// <param name="playerId"></param>
        void PlayerJoined(string playerId);

        /// <summary>
        /// Player has completed loading game data, bring player into campaign
        /// </summary>
        /// <param name="playerId"></param>
        void PlayerLoaded(string playerId);

        /// <summary>
        /// Have playerId enter campaign connections state from a mission state
        /// </summary>
        /// <param name="playerId"></param>
        void EnterCampaign(string playerId);

        /// <summary>
        /// Have a playerId enter mission state from a campaign state
        /// </summary>
        /// <param name="playerId"></param>
        void EnterMission(string playerId);
    }

    public class PlayerConnectionStatesManager : IPlayerConnectionStatesManager
    {
        private readonly IMessageBroker _messageBroker;

        public PlayerConnectionStatesManager(IMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;
        }

        public IDictionary<string, IConnectionLogic> ConnectionStates { get; private set; } = new Dictionary<string, IConnectionLogic>();

        public void AddNewPlayer(string playerId)
        {
            var connectionLogic = new ConnectionLogic(_messageBroker);
            ConnectionStates.Add(playerId, connectionLogic);
            connectionLogic.Join();
        }

        public void RemovePlayer(string playerId)
        {
            if (!ConnectionStates.ContainsKey(playerId))
                return;

            ConnectionStates.Remove(playerId);
        }

        public void PlayerJoined(string playerId)
        {
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.Load();
        }

        public void PlayerLoaded(string playerId)
        {
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.EnterCampaign();
        }

        public void EnterCampaign(string playerId)
        {
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.EnterCampaign();
        }

        public void EnterMission(string playerId)
        {
            if (!ConnectionStates.TryGetValue(playerId, out IConnectionLogic connectionLogic))
                return;

            connectionLogic.EnterMission();
        }
    }
}
