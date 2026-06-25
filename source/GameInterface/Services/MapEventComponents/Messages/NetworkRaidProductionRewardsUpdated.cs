using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventComponents.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRaidProductionRewardsUpdated : ICommand
{
    [ProtoMember(1)]
    public readonly string ComponentId;
    [ProtoMember(2)]
    public readonly string[] ItemIds;
    [ProtoMember(3)]
    public readonly float[] Values;

    public NetworkRaidProductionRewardsUpdated(string componentId, string[] itemIds, float[] values)
    {
        ComponentId = componentId;
        ItemIds = itemIds;
        Values = values;
    }
}