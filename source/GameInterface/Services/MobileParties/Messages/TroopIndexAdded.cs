using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Event sent when player recruits unit
    /// </summary>
    [BatchLogMessage]
    public record TroopIndexAdded : IEvent
    {
        public string PartyId { get; }
        public bool IsPrisonerRoster { get; }
        public int Index { get; }
        public int CountChange { get; }
        public int WoundedCountChange { get; }
        public int XpChange { get; }
        public bool RemoveDepleted { get; }

        public TroopIndexAdded(string partyId, 
            bool isPrisonerRoster, 
            int index, int countChange, 
            int woundedCountChange, 
            int xpChange, 
            bool removeDepleted)
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