using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Server -&gt; Client] Broadcast right before <see cref="NetworkBattleSimulationFinished"/>; applied only by
/// clients whose own party is among <see cref="Winners"/> (the pacer and any joined players on the winning side).
/// The client replays the defeated parties' casualties onto its own map event and applies the winning
/// <see cref="BattleState"/>, which lets the native <c>PlayerEncounter</c> result flow run locally in
/// player context — rolling the player-facing loot and opening the loot screen, the same path a real
/// (fought) battle uses. The simulation itself stays authoritative on the server.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleSimulationLoot : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly BattleState WinningState;
    [ProtoMember(3)]
    public readonly BattleSimDefeatedParty[] DefeatedParties;
    [ProtoMember(4)]
    public readonly BattleSimWinner[] Winners;

    public NetworkBattleSimulationLoot(string mapEventId, BattleState winningState, BattleSimDefeatedParty[] defeatedParties, BattleSimWinner[] winners)
    {
        MapEventId = mapEventId;
        WinningState = winningState;
        DefeatedParties = defeatedParties;
        Winners = winners;
    }
}
