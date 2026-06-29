using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add clan influence
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChangeClanInfluence : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerClanId;
    [ProtoMember(2)]
    public readonly int Influence;

    public NetworkChangeClanInfluence(string playerClanId, int influence)
    {
        PlayerClanId = playerClanId;
        Influence = influence;
    }
}
