using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.BattleRetreat.Messages;

/// <summary>
/// Client asks the server to apply a "Try to get away." retreat for its own party.
/// </summary>
/// <remarks>
/// Deliberately carries NOTHING about the outcome - no troop count, no item list, no position. The
/// client may only say "I clicked retreat, in this battle"; the server decides and replicates the cost,
/// so a client cannot dictate which of its troops die or how many.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBattleRetreat : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }

    /// <summary>Pins the battle the decision was made against, so a stale click cannot apply to a new one.</summary>
    [ProtoMember(2)]
    public string MapEventId { get; }

    public NetworkRequestBattleRetreat(string partyId, string mapEventId)
    {
        PartyId = partyId;
        MapEventId = mapEventId;
    }
}
