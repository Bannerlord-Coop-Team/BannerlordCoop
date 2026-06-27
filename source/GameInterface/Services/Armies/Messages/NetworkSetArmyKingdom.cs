using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add or remove kingdom from army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSetArmyKingdom : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;
    [ProtoMember(2)]
    public readonly string KingdomId;

    public NetworkSetArmyKingdom(string armyId, string kingdomId)
    {
        ArmyId = armyId;
        KingdomId = kingdomId;
    }
}
