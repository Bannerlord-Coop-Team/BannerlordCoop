using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.GameState.Messages
{
    /// <summary>
    /// When a player exits a game.
    /// </summary>
    public readonly struct GameExited : IEvent
    {
    }
}
