using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Client -> Server request for the server to authoritatively create the <see cref="TaleWorlds.CampaignSystem.MapEvents.MapEvent"/>
/// for a player's battle. The client blocks until it receives a matching <see cref="NetworkMapEventCreated"/> (correlated
/// by <see cref="RequestId"/>) or times out.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestCreateMapEvent : ICommand
{
    /// <summary>Correlation id used to match the response back to the blocked request.</summary>
    [ProtoMember(1)]
    public readonly string RequestId;
    [ProtoMember(2)]
    public readonly string AttackerId;
    [ProtoMember(3)]
    public readonly string DefenderId;

    [ProtoMember(4)]
    public readonly bool ForceRaid;
    [ProtoMember(5)]
    public readonly bool ForceSallyOut;
    [ProtoMember(6)]
    public readonly bool ForceVolunteers;
    [ProtoMember(7)]
    public readonly bool ForceSupplies;
    [ProtoMember(8)]
    public readonly bool IsSallyOutAmbush;
    [ProtoMember(9)]
    public readonly bool ForceBlockadeAttack;
    [ProtoMember(10)]
    public readonly bool ForceBlockadeSallyOutAttack;
    [ProtoMember(11)]
    public readonly bool ForceHideoutSendTroops;

    public NetworkRequestCreateMapEvent(string requestId, string attackerId, string defenderId, BattleCreationFlags flags)
    {
        RequestId = requestId;
        AttackerId = attackerId;
        DefenderId = defenderId;
        ForceRaid = flags.ForceRaid;
        ForceSallyOut = flags.ForceSallyOut;
        ForceVolunteers = flags.ForceVolunteers;
        ForceSupplies = flags.ForceSupplies;
        IsSallyOutAmbush = flags.IsSallyOutAmbush;
        ForceBlockadeAttack = flags.ForceBlockadeAttack;
        ForceBlockadeSallyOutAttack = flags.ForceBlockadeSallyOutAttack;
        ForceHideoutSendTroops = flags.ForceHideoutSendTroops;
    }

    public BattleCreationFlags Flags => new BattleCreationFlags(
        ForceRaid,
        ForceSallyOut,
        ForceVolunteers,
        ForceSupplies,
        IsSallyOutAmbush,
        ForceBlockadeAttack,
        ForceBlockadeSallyOutAttack,
        ForceHideoutSendTroops);
}
