using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Messages;

public readonly struct RequestRepairShipsAtPort : IEvent
{
    public readonly MobileParty MainParty;
    public readonly Settlement CurrentSettlement;

    public RequestRepairShipsAtPort(
        MobileParty mainParty,
        Settlement currentSettlement)
    {
        MainParty = mainParty;
        CurrentSettlement = currentSettlement;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestRepairShipsAtPort : ICommand
{
    [ProtoMember(1)]
    public readonly string MainPartyId;

    [ProtoMember(2)]
    public readonly uint CurrentSettlementHandle;

    public NetworkRequestRepairShipsAtPort(
        string mainPartyId,
        uint currentSettlementHandle)
    {
        MainPartyId = mainPartyId;
        CurrentSettlementHandle = currentSettlementHandle;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRepairShipsAtPortReply : ICommand {}