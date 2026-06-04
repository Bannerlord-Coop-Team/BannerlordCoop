using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract]
internal readonly struct NetworkAddXpToTroopIndex : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;
    [ProtoMember(2)]
    public readonly int Index;
    [ProtoMember(3)]
    public readonly int XpAmount;

    public NetworkAddXpToTroopIndex(string troopRosterId, int index, int xpAmount)
    {
        TroopRosterId = troopRosterId;
        Index = index;
        XpAmount = xpAmount;
    }
}
