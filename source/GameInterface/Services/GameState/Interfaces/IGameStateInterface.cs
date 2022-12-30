using GameInterface.Data;

namespace GameInterface.Services.GameState.Interfaces
{
    internal interface IGameStateInterface : IGameAbstraction
    {
        void EnterMainMenu();
        void StartCharacterCreation();
        void StartNewGame();
        void LoadSaveGame(IGameSaveData saveData);
        IGameSaveData PackageGameSaveData();
    }
}
