using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Messages.Game
{
    public readonly struct GoToMainMenuResponse : IMessage
    {
        bool Success { get; }
        public GoToMainMenuResponse(bool success)
        {
            Success = success;
        }
    }
}
