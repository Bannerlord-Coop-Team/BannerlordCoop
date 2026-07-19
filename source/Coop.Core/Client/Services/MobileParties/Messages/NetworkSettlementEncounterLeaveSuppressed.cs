using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Acknowledges that the server ignored a synthetic settlement leave.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkSettlementEncounterLeaveSuppressed : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;

    public NetworkSettlementEncounterLeaveSuppressed(string partyId)
    {
        PartyId = partyId;
    }
}
