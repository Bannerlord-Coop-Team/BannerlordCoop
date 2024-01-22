using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Event to tell GameInterface to handle unit being recruited
    /// </summary>
    public record AddTroopIndex : ICommand
    {
        public string PartyId { get; }
        public bool IsPrisonerRoster { get; }
        public int Index { get; }
        public int CountChange { get; }
        public int WoundedCountChange { get; }
        public int XpChange { get; }
        public bool RemoveDepleted { get; }

        public AddTroopIndex(string partyId, bool isPrisonerRoster, int index, int countChange, int woundedCountChange, int xpChange, bool removeDepleted)
        {
            PartyId = partyId;
            IsPrisonerRoster = isPrisonerRoster;
            Index = index;
            CountChange = countChange;
            WoundedCountChange = woundedCountChange;
            XpChange = xpChange;
            RemoveDepleted = removeDepleted;
        }
    }
}
