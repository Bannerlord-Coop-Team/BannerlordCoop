using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MapEvents.Messages
{
    public record SimulateBattleRound : ICommand
    {
        public string PartyId { get; }
        public int Side { get; }
        public float Advantage { get; }

        public SimulateBattleRound(string partyId, int side, float advantage)
        {
            PartyId = partyId;
            Side = side;
            Advantage = advantage;
        }
    }
}
