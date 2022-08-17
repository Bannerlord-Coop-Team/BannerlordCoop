using GameInterface.Services.GameState.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.GameState
{
    internal class GameStateService : IGameStateService
    {
        IEnterMainMenuHandler EnterMainMenuHandler { get; }
        IPackageGameSaveDataHandler PackageGameSaveDataHandler { get; }
        IStartCharacterCreationHandler StartCharacterCreationHandler { get; }
    }
}
