using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// A winning party's <c>ContributionToBattle</c>. The native loot/capture chance models drop any winner
/// whose contribution is 0; the simulation accumulates it on the server, so it must be carried to the
/// client (whose simulation engine is disabled) for the loot roll to assign it any share.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct BattleSimWinner
{
    [ProtoMember(1)]
    public readonly string PartyId;
    [ProtoMember(2)]
    public readonly int ContributionToBattle;

    public BattleSimWinner(string partyId, int contributionToBattle)
    {
        PartyId = partyId;
        ContributionToBattle = contributionToBattle;
    }
}
