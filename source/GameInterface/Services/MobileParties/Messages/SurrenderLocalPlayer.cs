using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Command to tell GameInterface for player surrender
    /// </summary>
    public record SurrenderLocalPlayer : ICommand
    {
        public string CaptorPartyId { get; }

        public SurrenderLocalPlayer(string captorPartyId)
        {
            CaptorPartyId = captorPartyId;
        }
    }
}
