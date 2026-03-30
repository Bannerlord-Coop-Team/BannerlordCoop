using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobilePartyAIs.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct UpdateAiBehaviorInteractablePoint : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyAiId;
    [ProtoMember(2)]
    public readonly string InteractablePointId;
    [ProtoMember(3)]
    public readonly bool IsNull;

    public UpdateAiBehaviorInteractablePoint(string mobilePartyAiId, string interactablePointId, bool isNull = false)
    {
        MobilePartyAiId = mobilePartyAiId;
        InteractablePointId = interactablePointId;
        IsNull = isNull;
    }
}
