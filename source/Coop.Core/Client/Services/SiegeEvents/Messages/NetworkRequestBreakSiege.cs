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

    /// <summary>
    /// Echoed back in the approval: whether the requester still needs its local encounter/menu
    /// finished (suppressed leave-menu flows) or already ran its native continuation (embedded
    /// camp writes such as try-to-get-away or the defeat path).
    /// </summary>
    [ProtoMember(2)]
    public bool FinishLocalMenus { get; }

    public NetworkRequestBreakSiege(string partyId, bool finishLocalMenus)
    {
        PartyId = partyId;
        FinishLocalMenus = finishLocalMenus;
    }
}
