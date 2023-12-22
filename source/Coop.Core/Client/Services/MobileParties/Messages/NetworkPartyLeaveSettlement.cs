using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Message from the server commanding a party to leave a settlement.
/// For all parties except the player party
/// </summary>
[ProtoContract(SkipConstructor = true)]
[DontLogMessage]
internal record NetworkPartyLeaveSettlement : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }

    public NetworkPartyLeaveSettlement(string partyId)
    {
        PartyId = partyId;
    }
}