using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct DonateItem : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetItemRosterId;

    [ProtoMember(2)]
    public readonly EquipmentElement EquipmentElement;

    [ProtoMember(3)]
    public readonly string PartyId;

    [ProtoMember(4)]
    public readonly TroopRosterElement TroopRosterElement;

    [ProtoMember(5)]
    public readonly int GainedXp;

    public DonateItem(
        string targetItemRosterId,
        EquipmentElement equipmentElement,
        string partyId,
        TroopRosterElement troopRosterElement,
        int gainedXp)
    {
        TargetItemRosterId = targetItemRosterId;
        EquipmentElement = equipmentElement;
        PartyId = partyId;
        TroopRosterElement = troopRosterElement;
        GainedXp = gainedXp;
    }
}