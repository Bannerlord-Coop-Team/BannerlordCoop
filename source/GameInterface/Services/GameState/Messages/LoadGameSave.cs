using Common.Messaging;
using GameInterface.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct LoadGameSave : ICommand
    {
        public IGameSaveData SaveData { get; }
    }

    public readonly struct GameSaveLoaded : IEvent
    {
    }
}
