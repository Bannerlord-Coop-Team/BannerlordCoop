using Common.Messaging;
using Coop.Core.Client.Services.MapEvents.Handlers;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Commands the clients to have a party leave a settlement
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkPartyLeaveSettlement : ICommand
{
    public string PartyId { get; }

    public NetworkPartyLeaveSettlement(string partyId)
    {
        PartyId = partyId;
    }
}