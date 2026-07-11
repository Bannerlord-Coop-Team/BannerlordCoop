using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// A siege dissolved without a battle; a client parked on the siege-preparation menus inside the
/// settlement switches to the vanilla end menu.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPromptSiegeEnded : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public bool BesiegerDefeated { get; }

    public NetworkPromptSiegeEnded(string settlementId, bool besiegerDefeated)
    {
        SettlementId = settlementId;
        BesiegerDefeated = besiegerDefeated;
    }
}
