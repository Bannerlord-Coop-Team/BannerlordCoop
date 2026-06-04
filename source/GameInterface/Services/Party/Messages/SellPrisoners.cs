using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct SellPrisoners : ICommand
{
    [ProtoMember(1)]
    public readonly string SellingPartyId;

    [ProtoMember(2)]
    public readonly TroopRosterData LeftPrisonerRosterData;

    public SellPrisoners(
        string sellingPartyId,
        TroopRosterData leftPrisonerRosterData)
    {
        SellingPartyId = sellingPartyId;
        LeftPrisonerRosterData = leftPrisonerRosterData;
    }
}