using Common.LogicStates;
using System;

namespace Coop.Core.Server.Connections
{
    public interface IConnectionState : IState, IDisposable
    {
        /// <summary>
        /// Player is in the process of creating a character
        /// </summary>
        void CreateCharacter();

        /// <summary>
        /// New character info is being transferred to server
        /// </summary>
        void TransferSave();

        /// <summary>
        /// Player loading server data as a whole
        /// </summary>
        void Load();

        /// <summary>
        /// Player entering into campaign map
        /// </summary>
        void EnterCampaign();

        /// <summary>
        /// Player entering mission state
        /// </summary>
        void EnterMission();
    }
}
