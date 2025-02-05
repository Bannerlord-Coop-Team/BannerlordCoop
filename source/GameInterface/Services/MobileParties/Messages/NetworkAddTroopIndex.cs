using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkAddTroopIndex : ICommand
    {
        [ProtoMember(1)]
        public string PartyId { get; }
        [ProtoMember(2)]
        public bool IsPrisonerRoster { get; }
        [ProtoMember(3)]
        public int Index { get; }
        [ProtoMember(4)]
        public int CountChange { get; }
        [ProtoMember(5)]
        public int WoundedCountChange { get; }
        [ProtoMember(6)]
        public int XpChange { get; }
        [ProtoMember(7)]
        public bool RemoveDepleted { get; }

        public NetworkAddTroopIndex(string partyId, bool isPrisonerRoster, int index, int countChange, int woundedCountChange, int xpChange, bool removeDepleted)
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