using Common.LogicStates;
using System;

namespace Coop.Core.Client.States
{
    public interface IClientState : IState, IDisposable
    {
        void Connect();
        void Disconnect();
        void StartCharacterCreation();
        void LoadSavedData();
        void ResolveNetworkGuids();
        void ExitGame();
        void EnterMainMenu(); 
        void EnterCampaignState();
        void EnterMissionState();
    }
}
