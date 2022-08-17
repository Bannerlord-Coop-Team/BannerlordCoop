using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.GameState.Messages
{
    /// <summary>
    /// Goes to the main menu from any game state.
    /// </summary>
    public readonly struct EnterMainMenuCommand : ICommand
    {
    }

    /// <summary>
    /// Reply to <seealso cref="EnterMainMenuCommand"/>.
    /// </summary>
    public readonly struct MainMenuEntered : ICommand
    {
    }
}
