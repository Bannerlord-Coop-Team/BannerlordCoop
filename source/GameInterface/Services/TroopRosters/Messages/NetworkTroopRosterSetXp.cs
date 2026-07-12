using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Legacy single-operation form for setting one identity-keyed roster element's XP. Normal authority
/// traffic carries this operation inside <see cref="NetworkTroopRosterElementBatch"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterSetXp : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;
    [ProtoMember(2)]
    public readonly string CharacterId;
    [ProtoMember(3)]
    public readonly int Xp;

    public NetworkTroopRosterSetXp(string rosterId, string characterId, int xp)
    {
        RosterId = rosterId;
        CharacterId = characterId;
        Xp = xp;
    }
}
