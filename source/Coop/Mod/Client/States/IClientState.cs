using Common.LogicStates;

namespace Coop.Mod.LogicStates.Client
{
    public interface IClientState : IState
    {
        void Connect();
        void Disconnect();
        void StartCharacterCreation();
        void LoadSavedData();
        void ExitGame();
        void EnterMainMenu();
    }
}
