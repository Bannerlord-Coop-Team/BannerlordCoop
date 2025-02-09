using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkUpdateTroopRosterData : ICommand
    {
        [ProtoMember(1)]
        public string RosterId { get; }

        [ProtoMember(2)]
        public string CharacterId { get; }
        [ProtoMember(3)]
        public int DeltaXp { get; }
        [ProtoMember(4)]
        public int Number { get; }
        [ProtoMember(5)]
        public int WoundedNumber { get; }
        [ProtoMember(6)]
        public int Xp { get; }
        [ProtoMember(7)]
        public int Index { get; }

        public NetworkUpdateTroopRosterData(string rosterId, string characterId, int deltaXp, int number, int woundedNumber, int xp, int index)
        {
            RosterId = rosterId;
            CharacterId = characterId;
            DeltaXp = deltaXp;
            Number = number;
            WoundedNumber = woundedNumber;
            Xp = xp;
            Index = index;
        }
    }
}
