using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

/// <summary>
/// Client asks the server to remove its party from its siege camp.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBreakSiege : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }

    public NetworkRequestBreakSiege(string partyId)
    {
        PartyId = partyId;
    }
}
