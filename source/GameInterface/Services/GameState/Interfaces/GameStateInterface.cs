using GameInterface.Data;
using GameInterface.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.GameState.Interfaces
{
    internal class GameStateInterface : IGameStateInterface
    {
        public void EnterMainMenu()
        {
            throw new NotImplementedException();
        }

        public void LoadSaveGame(IGameSaveData saveData)
        {
            throw new NotImplementedException();
        }

        public IGameSaveData PackageGameSaveData()
        {
            throw new NotImplementedException();
        }

        public void StartCharacterCreation()
        {
            throw new NotImplementedException();
        }

        public void StartNewGame()
        {
            throw new NotImplementedException();
        }
    }
}
