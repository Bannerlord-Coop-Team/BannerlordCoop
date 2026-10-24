using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct BattleSurgeryReward : IEvent
{
    public readonly MobileParty Party;
    public readonly MapEvent MapEvent;
    public readonly bool SurgerySuccess;
    public readonly int TroopTier;

    public BattleSurgeryReward(MobileParty party, bool surgerySuccess, int troopTier)
    {
        Party = party;
        MapEvent = party.MapEvent;
        SurgerySuccess = surgerySuccess;
        TroopTier = troopTier;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleSurgeryReward : ICommand
{
    [ProtoMember(1)] public readonly string PartyId;
    [ProtoMember(2)] public readonly string MapEventId;
    [ProtoMember(3)] public readonly bool SurgerySuccess;
    [ProtoMember(4)] public readonly int TroopTier;

    public NetworkBattleSurgeryReward(string partyId, string mapEventId, bool surgerySuccess, int troopTier)
    {
        PartyId = partyId;
        MapEventId = mapEventId;
        SurgerySuccess = surgerySuccess;
        TroopTier = troopTier;
    }
}
