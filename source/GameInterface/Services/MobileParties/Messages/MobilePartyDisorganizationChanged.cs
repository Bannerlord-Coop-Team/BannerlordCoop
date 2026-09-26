using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

internal readonly struct MobilePartyDisorganizationChanged : IEvent
{
    public readonly MobileParty Party;
    public readonly bool IsDisorganized;

    public MobilePartyDisorganizationChanged(MobileParty party, bool isDisorganized)
    {
        Party = party;
        IsDisorganized = isDisorganized;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMobilePartyDisorganizationChanged : ICommand
{
    [ProtoMember(1)]
    public readonly uint PartyId;
    [ProtoMember(2)]
    public readonly bool IsDisorganized;

    public NetworkMobilePartyDisorganizationChanged(uint partyId, bool isDisorganized)
    {
        PartyId = partyId;
        IsDisorganized = isDisorganized;
    }
}
