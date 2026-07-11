using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// The server parked a player-led siege aftermath; the leading player's client opens the choice menu
/// if its own encounter flow hasn't already.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkPromptSiegeAftermathChoice : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public string LeaderPartyId { get; }

    public NetworkPromptSiegeAftermathChoice(string settlementId, string leaderPartyId)
    {
        SettlementId = settlementId;
        LeaderPartyId = leaderPartyId;
    }
}
