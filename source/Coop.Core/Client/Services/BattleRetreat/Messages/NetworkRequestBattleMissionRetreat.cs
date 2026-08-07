using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.BattleRetreat.Messages;

/// <summary>
/// Client tells the server it left a battle mission without resolving the battle, so its party should
/// leave the battle too.
/// </summary>
/// <remarks>
/// Carries only the two ids. The server re-derives ownership from the peer and re-checks the battle is
/// still unresolved, so a client can neither pull another player's party out nor use this to end a
/// battle that has already produced a result.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBattleMissionRetreat : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }

    /// <summary>Pins the battle that was left, so a stale message cannot apply to a later one.</summary>
    [ProtoMember(2)]
    public string MapEventId { get; }

    public NetworkRequestBattleMissionRetreat(string partyId, string mapEventId)
    {
        PartyId = partyId;
        MapEventId = mapEventId;
    }
}
