using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddToCountsAtIndex : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly int Index;

    [ProtoMember(3)]
    public readonly int CountChange;

    [ProtoMember(4)]
    public readonly int WoundedCountChange;

    [ProtoMember(5)]
    public readonly int XpChange;

    [ProtoMember(6)]
    public readonly bool RemoveDepleted;

    public NetworkAddToCountsAtIndex(
        string troopRosterId,
        int index,
        int countChange,
        int woundedCountChange,
        int xpChange,
        bool removeDepleted)
    {
        TroopRosterId = troopRosterId;
        Index = index;
        CountChange = countChange;
        WoundedCountChange = woundedCountChange;
        XpChange = xpChange;
        RemoveDepleted = removeDepleted;
    }
}
