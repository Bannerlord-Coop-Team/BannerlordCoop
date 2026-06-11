using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// A single <c>IBattleObserver.TroopNumberChanged</c> callback captured on the server during one
/// simulation round. The six integers are forwarded positionally in interface order
/// (number, numberKilled, numberWounded, numberRouted, killCount, numberReadyToUpgrade).
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct BattleSimTroopChange
{
    [ProtoMember(1)]
    public readonly int Side;
    [ProtoMember(2)]
    public readonly string PartyId;
    [ProtoMember(3)]
    public readonly string CharacterId;
    [ProtoMember(4)]
    public readonly bool IsHero;
    [ProtoMember(5)]
    public readonly int Number;
    [ProtoMember(6)]
    public readonly int NumberKilled;
    [ProtoMember(7)]
    public readonly int NumberWounded;
    [ProtoMember(8)]
    public readonly int NumberRouted;
    [ProtoMember(9)]
    public readonly int KillCount;
    [ProtoMember(10)]
    public readonly int NumberReadyToUpgrade;

    public BattleSimTroopChange(int side, string partyId, string characterId, bool isHero,
        int number, int numberKilled, int numberWounded, int numberRouted, int killCount, int numberReadyToUpgrade)
    {
        Side = side;
        PartyId = partyId;
        CharacterId = characterId;
        IsHero = isHero;
        Number = number;
        NumberKilled = numberKilled;
        NumberWounded = numberWounded;
        NumberRouted = numberRouted;
        KillCount = killCount;
        NumberReadyToUpgrade = numberReadyToUpgrade;
    }
}

/// <summary>
/// [Server -> Client] One round's worth of scoreboard updates from the authoritative simulation.
/// The client replays them onto its open simulation screen, paced by its own frame tick, so the
/// auto-resolve plays out instead of snapping to the result.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleSimulationRound : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly BattleSimTroopChange[] Changes;

    public NetworkBattleSimulationRound(string mapEventId, BattleSimTroopChange[] changes)
    {
        MapEventId = mapEventId;
        Changes = changes;
    }
}
