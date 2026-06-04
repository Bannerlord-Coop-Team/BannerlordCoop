using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Party.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct SellPrisoners : ICommand
{
    [ProtoMember(1)]
    public readonly string SellingPartyId;

    [ProtoMember(2)]
    public readonly List<(string, int, int, int)> LeftPrisonerRosterData;

    public SellPrisoners(
        string sellingPartyId,
        List<(string, int, int, int)> leftPrisonerRosterData)
    {
        SellingPartyId = sellingPartyId;
        LeftPrisonerRosterData = leftPrisonerRosterData;
    }
}