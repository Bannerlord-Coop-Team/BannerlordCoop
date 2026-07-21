using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Actions.Messages;

/// <summary>
/// Command to disband army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkDisbandArmyApplyInternal : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;
    [ProtoMember(2)]
    public readonly string ClientPartyId;

    public NetworkDisbandArmyApplyInternal(string armyId, string clientPartyId)
    {
        ArmyId = armyId;
        ClientPartyId = clientPartyId;
    }
}
