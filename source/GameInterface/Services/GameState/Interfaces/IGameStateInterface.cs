using GameInterface.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.GameState.Interfaces
{
    internal interface IGameStateInterface
    {
        void EnterMainMenu();
        void StartCharacterCreation();
        void StartNewGame();
        void LoadSaveGame(IGameSaveData saveData);
        IGameSaveData PackageGameSaveData();
    }
}
