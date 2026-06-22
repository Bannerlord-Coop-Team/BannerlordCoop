using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>A defeated party with the troops it lost in the simulation.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct BattleSimDefeatedParty
{
    [ProtoMember(1)]
    public readonly string PartyId;
    [ProtoMember(2)]
    public readonly BattleSimCasualty[] Died;
    [ProtoMember(3)]
    public readonly BattleSimCasualty[] Wounded;

    public BattleSimDefeatedParty(string partyId, BattleSimCasualty[] died, BattleSimCasualty[] wounded)
    {
        PartyId = partyId;
        Died = died;
        Wounded = wounded;
    }
}
