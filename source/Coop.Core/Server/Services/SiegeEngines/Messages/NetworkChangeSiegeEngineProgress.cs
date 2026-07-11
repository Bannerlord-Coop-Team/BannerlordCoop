using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEngines.Messages;

/// <summary>
/// Notify clients of a siege engine construction or redeployment progress value.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSiegeEngineProgress : IEvent
{
    [ProtoMember(1)]
    public string SiegeEngineId { get; }
    [ProtoMember(2)]
    public bool IsRedeployment { get; }
    [ProtoMember(3)]
    public float Value { get; }

    public NetworkChangeSiegeEngineProgress(string siegeEngineId, bool isRedeployment, float value)
    {
        SiegeEngineId = siegeEngineId;
        IsRedeployment = isRedeployment;
        Value = value;
    }
}
